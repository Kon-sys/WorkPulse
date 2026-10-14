SET search_path=workpulse,public;
CREATE FUNCTION check_analysis_bounds() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE r workpulse.analytics_runs%ROWTYPE;
BEGIN
    SELECT * INTO r FROM workpulse.analytics_runs WHERE organization_id=NEW.organization_id AND id=NEW.run_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Analysis run not found' USING ERRCODE='23503'; END IF;
    IF NEW.started_at < r.period_start OR NEW.ended_at > r.period_end THEN
        RAISE EXCEPTION 'Interval is outside analysis period' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER analysis_interval_bounds BEFORE INSERT OR UPDATE ON activity_intervals FOR EACH ROW EXECUTE FUNCTION check_analysis_bounds();
CREATE FUNCTION protect_analysis_bounds() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT FROM workpulse.activity_intervals WHERE organization_id=OLD.organization_id AND run_id=OLD.id
        AND (started_at < NEW.period_start OR ended_at > NEW.period_end)) THEN
        RAISE EXCEPTION 'Analysis period excludes existing intervals' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER analysis_run_bounds BEFORE UPDATE OF period_start,period_end ON analytics_runs FOR EACH ROW EXECUTE FUNCTION protect_analysis_bounds();
CREATE FUNCTION check_interval_source() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE source_employee uuid; interval_employee uuid; source_session uuid; target_session uuid;
BEGIN
    SELECT employee_id,session_id INTO source_employee,source_session FROM workpulse.activity_events
    WHERE organization_id=NEW.organization_id AND id=NEW.event_id FOR SHARE;
    SELECT session_id INTO target_session FROM workpulse.activity_intervals
    WHERE organization_id=NEW.organization_id AND id=NEW.interval_id FOR SHARE;
    SELECT employee_id INTO interval_employee FROM workpulse.work_sessions
    WHERE organization_id=NEW.organization_id AND id=target_session FOR SHARE;
    IF source_employee IS DISTINCT FROM interval_employee OR (source_session IS NOT NULL AND source_session <> target_session) THEN
        RAISE EXCEPTION 'Source event belongs to another employee or session' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER interval_source_employee BEFORE INSERT OR UPDATE ON interval_sources FOR EACH ROW EXECUTE FUNCTION check_interval_source();
-- Preserve enrollment/history identity. Re-enrollment uses a new device identifier.
CREATE FUNCTION protect_event_identity() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_TABLE_NAME='activity_events' AND (NEW.organization_id,NEW.id,NEW.employee_id,NEW.device_id,NEW.session_id,NEW.client_event_id,NEW.occurred_at)
        IS DISTINCT FROM (OLD.organization_id,OLD.id,OLD.employee_id,OLD.device_id,OLD.session_id,OLD.client_event_id,OLD.occurred_at) THEN
        RAISE EXCEPTION 'Event identity/time/session is immutable; ingest with session resolved or NULL' USING ERRCODE='23514';
    ELSIF TG_TABLE_NAME='activity_intervals' AND (NEW.organization_id,NEW.id,NEW.session_id)
        IS DISTINCT FROM (OLD.organization_id,OLD.id,OLD.session_id) THEN
        RAISE EXCEPTION 'Interval identity/session is immutable' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER immutable_event_identity BEFORE UPDATE ON activity_events FOR EACH ROW EXECUTE FUNCTION protect_event_identity();
CREATE TRIGGER immutable_interval_identity BEFORE UPDATE ON activity_intervals FOR EACH ROW EXECUTE FUNCTION protect_event_identity();
-- Index every composite child FK not already covered by a leading index.
DO $$ DECLARE c record; columns text;
BEGIN
    FOR c IN SELECT conname,conrelid,conkey FROM pg_constraint WHERE contype='f'
        AND connamespace='workpulse'::regnamespace LOOP
        IF NOT EXISTS (SELECT FROM pg_index i WHERE i.indrelid=c.conrelid AND i.indisvalid
            AND (i.indkey::smallint[])[0:cardinality(c.conkey)-1] = c.conkey) THEN
            SELECT string_agg(quote_ident(a.attname),',' ORDER BY k.position) INTO columns
            FROM unnest(c.conkey) WITH ORDINALITY k(number,position)
            JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.number;
            EXECUTE format('CREATE INDEX %I ON %s (%s)',left(c.conname,55)||'_idx',c.conrelid::regclass,columns);
        END IF;
    END LOOP;
END $$;
REVOKE ALL ON FUNCTION check_analysis_bounds(),protect_analysis_bounds(),check_interval_source(),protect_event_identity() FROM PUBLIC;
