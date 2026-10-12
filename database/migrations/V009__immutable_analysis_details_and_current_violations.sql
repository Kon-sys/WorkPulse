SET search_path=workpulse,public;
ALTER TABLE violations ADD COLUMN latest_run_id uuid;
ALTER TABLE violations ADD CONSTRAINT violation_latest_run_fk FOREIGN KEY(organization_id,latest_run_id) REFERENCES analytics_runs(organization_id,id);
CREATE INDEX violations_latest_run_idx ON violations(organization_id,latest_run_id);
CREATE FUNCTION protect_completed_intervals() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP<>'INSERT' THEN
  PERFORM 1 FROM analytics_runs WHERE organization_id=OLD.organization_id AND id=OLD.run_id AND status='completed' FOR SHARE;
  IF FOUND THEN RAISE EXCEPTION 'Completed analysis details are immutable' USING ERRCODE='23514'; END IF;
 END IF;
 IF TG_OP<>'DELETE' THEN
  PERFORM 1 FROM analytics_runs WHERE organization_id=NEW.organization_id AND id=NEW.run_id AND status='completed' FOR SHARE;
  IF FOUND THEN RAISE EXCEPTION 'Completed analysis details are immutable' USING ERRCODE='23514'; END IF;
 END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END $$;
CREATE TRIGGER immutable_completed_intervals BEFORE INSERT OR UPDATE OR DELETE ON activity_intervals
 FOR EACH ROW EXECUTE FUNCTION protect_completed_intervals();
CREATE FUNCTION protect_completed_sources() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP<>'INSERT' THEN
  PERFORM 1 FROM analytics_runs r JOIN activity_intervals i ON i.organization_id=r.organization_id AND i.run_id=r.id
   WHERE i.organization_id=OLD.organization_id AND i.id=OLD.interval_id AND r.status='completed' FOR SHARE OF r;
  IF FOUND THEN RAISE EXCEPTION 'Completed analysis sources are immutable' USING ERRCODE='23514'; END IF;
 END IF;
 IF TG_OP<>'DELETE' THEN
  PERFORM 1 FROM analytics_runs r JOIN activity_intervals i ON i.organization_id=r.organization_id AND i.run_id=r.id
   WHERE i.organization_id=NEW.organization_id AND i.id=NEW.interval_id AND r.status='completed' FOR SHARE OF r;
  IF FOUND THEN RAISE EXCEPTION 'Completed analysis sources are immutable' USING ERRCODE='23514'; END IF;
 END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END $$;
CREATE TRIGGER immutable_completed_sources BEFORE INSERT OR UPDATE OR DELETE ON interval_sources
 FOR EACH ROW EXECUTE FUNCTION protect_completed_sources();
REVOKE ALL ON FUNCTION protect_completed_intervals(),protect_completed_sources() FROM PUBLIC;
