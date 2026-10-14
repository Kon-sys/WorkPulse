SET search_path = workpulse, public;
CREATE TABLE audit_log (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    actor_id uuid,
    table_name text NOT NULL,
    operation text NOT NULL CHECK (operation IN ('INSERT','UPDATE','DELETE')),
    entity_id uuid NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    before_data jsonb,
    after_data jsonb,
    PRIMARY KEY (organization_id,id),
    FOREIGN KEY (organization_id,actor_id) REFERENCES user_accounts(organization_id,id)
);
CREATE FUNCTION current_organization() RETURNS uuid LANGUAGE sql STABLE
SET search_path=pg_catalog AS $$ SELECT nullif(current_setting('app.organization_id',true),'')::uuid $$;
DO $$ DECLARE t record; expression text;
BEGIN
    FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='workpulse' AND tablename NOT IN ('roles','flyway_schema_history') LOOP
        expression := CASE WHEN t.tablename='organizations' THEN 'id' ELSE 'organization_id' END;
        EXECUTE format('ALTER TABLE workpulse.%I ENABLE ROW LEVEL SECURITY',t.tablename);
        EXECUTE format('ALTER TABLE workpulse.%I FORCE ROW LEVEL SECURITY',t.tablename);
        EXECUTE format('CREATE POLICY tenant_isolation ON workpulse.%I USING (%s = workpulse.current_organization()) WITH CHECK (%s = workpulse.current_organization())',t.tablename,expression,expression);
    END LOOP;
END $$;
CREATE FUNCTION write_audit() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,workpulse AS $$
DECLARE r record;
BEGIN
    IF TG_OP='DELETE' THEN r := OLD; ELSE r := NEW; END IF;
    INSERT INTO workpulse.audit_log(organization_id,actor_id,table_name,operation,entity_id,before_data,after_data)
    VALUES(r.organization_id,nullif(current_setting('app.actor_id',true),'')::uuid,TG_TABLE_NAME,TG_OP,r.id,
        CASE WHEN TG_OP<>'INSERT' THEN to_jsonb(OLD) END,CASE WHEN TG_OP<>'DELETE' THEN to_jsonb(NEW) END);
    RETURN NULL;
END $$;
DO $$ DECLARE name text;
BEGIN
    FOREACH name IN ARRAY ARRAY['monitoring_policies','policy_versions','schedule_days','classification_rules','policy_assignments','role_assignments'] LOOP
        EXECUTE format('CREATE TRIGGER audit_changes AFTER INSERT OR UPDATE OR DELETE ON workpulse.%I FOR EACH ROW EXECUTE FUNCTION workpulse.write_audit()',name);
    END LOOP;
END $$;
REVOKE ALL ON SCHEMA workpulse FROM PUBLIC;
GRANT USAGE ON SCHEMA workpulse TO workpulse_app;
GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA workpulse TO workpulse_app;
REVOKE ALL ON flyway_schema_history FROM workpulse_app;
REVOKE INSERT,UPDATE,DELETE ON roles,audit_log FROM workpulse_app;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA workpulse FROM PUBLIC;
GRANT EXECUTE ON FUNCTION current_organization() TO workpulse_app;
ALTER ROLE workpulse_app SET search_path=workpulse,pg_catalog;
CREATE INDEX audit_time_idx ON audit_log(organization_id,occurred_at DESC);
