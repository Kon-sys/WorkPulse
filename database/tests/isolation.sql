-- Executed over TCP with the actual limited application login, never the owner.
\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
IF current_user <> 'workpulse_app' THEN RAISE EXCEPTION 'Wrong test role'; END IF;
IF EXISTS(SELECT FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolbypassrls)) THEN RAISE EXCEPTION 'Unsafe application role'; END IF;
IF EXISTS(SELECT FROM workpulse.organizations) THEN RAISE EXCEPTION 'Missing context leaked tenants'; END IF;
BEGIN
    INSERT INTO workpulse.departments(organization_id,code,name) VALUES('10000000-0000-0000-0000-000000000001','NO-CONTEXT','No context');
    RAISE EXCEPTION 'Missing context write succeeded';
EXCEPTION WHEN insufficient_privilege THEN NULL;
END;
END $$;
SELECT set_config('app.organization_id','10000000-0000-0000-0000-000000000001',true);
SELECT set_config('app.actor_id','10000000-0000-0000-0000-000000000001',true);
DO $$ DECLARE affected integer;
BEGIN
    IF (SELECT count(*) FROM workpulse.organizations) <> 1 OR (SELECT count(*) FROM workpulse.employees) <> 1 THEN RAISE EXCEPTION 'Isolation failed'; END IF;
    UPDATE workpulse.employees SET full_name='Attack' WHERE organization_id='20000000-0000-0000-0000-000000000001';
    GET DIAGNOSTICS affected=ROW_COUNT;
    IF affected<>0 THEN RAISE EXCEPTION 'Cross-tenant update succeeded'; END IF;
    BEGIN
        INSERT INTO workpulse.departments(organization_id,code,name) VALUES('20000000-0000-0000-0000-000000000001','ATTACK','Attack');
        RAISE EXCEPTION 'Cross-tenant insert succeeded';
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    BEGIN
        DELETE FROM workpulse.audit_log;
        RAISE EXCEPTION 'Audit deletion succeeded';
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    BEGIN
        CREATE TABLE workpulse.attack(id integer);
        RAISE EXCEPTION 'DDL succeeded';
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    INSERT INTO workpulse.departments(organization_id,code,name) VALUES('10000000-0000-0000-0000-000000000001','TEST-OWN','Own department');
    INSERT INTO workpulse.monitoring_policies(organization_id,name) VALUES('10000000-0000-0000-0000-000000000001','TEST-AUDIT');
    IF NOT EXISTS(SELECT FROM workpulse.audit_log WHERE table_name='monitoring_policies' AND after_data->>'name'='TEST-AUDIT' AND actor_id='10000000-0000-0000-0000-000000000001') THEN
        RAISE EXCEPTION 'App write did not generate audit with actor';
    END IF;
END $$;
ROLLBACK;
DO $$ BEGIN
IF EXISTS(SELECT FROM workpulse.organizations) THEN RAISE EXCEPTION 'Transaction context leaked'; END IF;
END $$;
\echo SaaS isolation checks passed using workpulse_app over TCP.
