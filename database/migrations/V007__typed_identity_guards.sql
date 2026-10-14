SET search_path=workpulse,public;
CREATE OR REPLACE FUNCTION protect_event_identity() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_TABLE_NAME='activity_events' THEN
        IF (NEW.organization_id,NEW.id,NEW.employee_id,NEW.device_id,NEW.session_id,NEW.client_event_id,NEW.occurred_at)
            IS DISTINCT FROM (OLD.organization_id,OLD.id,OLD.employee_id,OLD.device_id,OLD.session_id,OLD.client_event_id,OLD.occurred_at) THEN
            RAISE EXCEPTION 'Event identity/time/session is immutable; ingest with session resolved or NULL' USING ERRCODE='23514';
        END IF;
    ELSE
        IF (NEW.organization_id,NEW.id,NEW.session_id) IS DISTINCT FROM (OLD.organization_id,OLD.id,OLD.session_id) THEN
            RAISE EXCEPTION 'Interval identity/session is immutable' USING ERRCODE='23514';
        END IF;
    END IF;
    RETURN NEW;
END $$;
