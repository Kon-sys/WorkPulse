SET search_path=workpulse,public;
ALTER TABLE user_accounts ADD COLUMN password_hash text;
ALTER TABLE user_accounts ADD COLUMN security_stamp uuid NOT NULL DEFAULT gen_random_uuid();
ALTER TABLE user_accounts ADD COLUMN failed_logins integer NOT NULL DEFAULT 0 CHECK (failed_logins >= 0);
ALTER TABLE user_accounts ADD COLUMN locked_until timestamptz;
CREATE TABLE device_credentials (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL,
    device_id uuid NOT NULL,
    token_hash text NOT NULL CHECK (length(token_hash)=64),
    created_at timestamptz NOT NULL DEFAULT now(),
    revoked_at timestamptz,
    PRIMARY KEY(organization_id,id),
    FOREIGN KEY(organization_id,device_id) REFERENCES devices(organization_id,id)
);
CREATE INDEX device_credentials_device_idx ON device_credentials(organization_id,device_id);
ALTER TABLE device_credentials ENABLE ROW LEVEL SECURITY;
ALTER TABLE device_credentials FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON device_credentials
 USING (organization_id=workpulse.current_organization())
 WITH CHECK (organization_id=workpulse.current_organization());
GRANT SELECT,INSERT,UPDATE,DELETE ON device_credentials TO workpulse_app;
ALTER TABLE analytics_runs ADD COLUMN session_id uuid;
ALTER TABLE analytics_runs ADD COLUMN policy_version_id uuid;
ALTER TABLE analytics_runs ADD COLUMN signal_lifetime_seconds integer CHECK(signal_lifetime_seconds BETWEEN 1 AND 86400);
ALTER TABLE analytics_runs ADD COLUMN result_snapshot jsonb;
ALTER TABLE analytics_runs ADD CONSTRAINT run_session_fk FOREIGN KEY(organization_id,session_id) REFERENCES work_sessions(organization_id,id);
ALTER TABLE analytics_runs ADD CONSTRAINT run_policy_fk FOREIGN KEY(organization_id,policy_version_id) REFERENCES policy_versions(organization_id,id);
CREATE INDEX analytics_session_idx ON analytics_runs(organization_id,session_id,created_at DESC);
CREATE FUNCTION protect_completed_run() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF OLD.status='completed' THEN
  RAISE EXCEPTION 'Completed analysis runs are immutable' USING ERRCODE='23514';
 END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END $$;
CREATE TRIGGER completed_run_immutable BEFORE UPDATE OR DELETE ON analytics_runs
 FOR EACH ROW EXECUTE FUNCTION protect_completed_run();
REVOKE ALL ON FUNCTION protect_completed_run() FROM PUBLIC;
