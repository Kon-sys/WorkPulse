\set ON_ERROR_STOP on
BEGIN;
SET search_path=workpulse,public;
CREATE FUNCTION pg_temp.expect_error(statement text,expected_state text) RETURNS void LANGUAGE plpgsql AS $$
DECLARE actual text;
BEGIN
    BEGIN
        EXECUTE statement;
    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS actual=RETURNED_SQLSTATE;
        IF actual <> expected_state THEN RAISE EXCEPTION 'Expected %, got %: %',expected_state,actual,SQLERRM; END IF;
        RETURN;
    END;
    RAISE EXCEPTION 'Statement unexpectedly succeeded: %',statement;
END $$;
SELECT pg_temp.expect_error($q$INSERT INTO employees(organization_id,department_id,employee_number,full_name,hired_on)
VALUES('10000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','CROSS','Bad','2026-01-01')$q$,'23503');
SELECT pg_temp.expect_error($q$INSERT INTO work_sessions(organization_id,employee_id,department_id,policy_version_id,started_at,ended_at,end_reason)
VALUES('10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','2026-10-12T07:00Z','2026-10-12T08:00Z','manual')$q$,'23P01');
SELECT pg_temp.expect_error($q$INSERT INTO session_breaks(organization_id,session_id,kind,source,started_at,ended_at)
VALUES('10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','lunch','manual','2026-10-12T11:00Z','2026-10-12T11:30Z')$q$,'23505');
SELECT pg_temp.expect_error($q$INSERT INTO session_breaks(organization_id,session_id,kind,source,started_at,ended_at)
VALUES('10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','rest','manual','2026-10-12T09:10Z','2026-10-12T09:20Z')$q$,'23P01');
SELECT pg_temp.expect_error($q$UPDATE work_sessions SET ended_at='2026-10-12T09:30Z' WHERE organization_id='10000000-0000-0000-0000-000000000001'$q$,'23514');
SELECT pg_temp.expect_error($q$UPDATE policy_versions SET idle_limit_seconds=1 WHERE organization_id='10000000-0000-0000-0000-000000000001'$q$,'23514');
SELECT pg_temp.expect_error($q$UPDATE classification_rules SET category='neutral' WHERE organization_id='10000000-0000-0000-0000-000000000001'$q$,'23514');
SELECT pg_temp.expect_error($q$UPDATE organizations SET time_zone='Invented/Zone' WHERE slug='demo-a'$q$,'23514');
SELECT pg_temp.expect_error($q$INSERT INTO activity_events(organization_id,employee_id,device_id,client_event_id,event_type,occurred_at)
VALUES('10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','focus','2026-10-12T07:00Z')$q$,'23505');
SELECT pg_temp.expect_error($q$INSERT INTO activity_intervals(organization_id,run_id,session_id,category,started_at,ended_at)
VALUES('10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','idle','2026-10-12T08:00Z','2026-10-12T10:00Z')$q$,'23P01');
-- Night shifts and multiple devices are valid.
SELECT pg_temp.expect_error($q$UPDATE analytics_runs SET period_start='2026-10-12T07:00Z' WHERE organization_id='10000000-0000-0000-0000-000000000001'$q$,'23514');
SELECT pg_temp.expect_error($q$UPDATE activity_intervals SET ended_at='2026-10-12T16:00Z' WHERE organization_id='10000000-0000-0000-0000-000000000001'$q$,'23514');
SELECT pg_temp.expect_error($q$UPDATE activity_events SET occurred_at='2026-10-12T08:00Z' WHERE organization_id='10000000-0000-0000-0000-000000000001'$q$,'23514');
UPDATE activity_intervals SET category='neutral' WHERE organization_id='10000000-0000-0000-0000-000000000001';
INSERT INTO work_sessions(organization_id,employee_id,department_id,policy_version_id,started_at,ended_at,end_reason)
VALUES('10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','2026-10-13T20:00Z','2026-10-14T04:00Z','schedule');
INSERT INTO devices(organization_id,employee_id,name,platform)
VALUES('10000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','SECOND-PC','linux');
DO $$ BEGIN
IF NOT EXISTS (SELECT FROM audit_log WHERE table_name='policy_versions' AND operation='UPDATE') THEN RAISE EXCEPTION 'Audit not generated'; END IF;
END $$;
\echo Integrity checks passed (13 expected failures + valid interval update, night shift, second device, audit).
ROLLBACK;
