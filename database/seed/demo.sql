-- Synthetic data only; repeatable, separate from migrations.
\set ON_ERROR_STOP on
BEGIN;
SET search_path=workpulse,public;
INSERT INTO organizations(id,slug,name,time_zone) VALUES
('10000000-0000-0000-0000-000000000001','demo-a','Демо: Север','Europe/Minsk'),
('20000000-0000-0000-0000-000000000001','demo-b','Демо: Юг','Europe/Minsk') ON CONFLICT DO NOTHING;
INSERT INTO departments(organization_id,id,code,name)
SELECT id,id,'DEV','Разработка' FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO employees(organization_id,id,department_id,employee_number,full_name,hired_on)
SELECT id,id,id,'E001',CASE slug WHEN 'demo-a' THEN 'Алексей Демо' ELSE 'Мария Демо' END,'2026-01-01'
FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO user_accounts(organization_id,id,employee_id,email)
SELECT id,id,id,slug || '@example.test' FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO role_assignments(organization_id,id,user_id,role_code,scope)
SELECT id,id,id,'hr','organization' FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO devices(organization_id,id,employee_id,name,platform)
SELECT id,id,id,'DEMO-PC','windows' FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO monitoring_policies(organization_id,id,name)
SELECT id,id,'Демонстрационная политика' FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO policy_versions(organization_id,id,policy_id,version_number,idle_limit_seconds,non_target_limit_seconds,lunch_limit_seconds,time_zone)
SELECT id,id,id,1,300,900,3600,'Europe/Minsk' FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
-- Published content is not touched on repeated runs.
INSERT INTO schedule_days(organization_id,id,policy_version_id,iso_weekday,starts_at,ends_at)
SELECT o.id,o.id,o.id,1,'09:00','18:00' FROM organizations o
JOIN policy_versions v ON v.organization_id=o.id AND v.id=o.id
WHERE o.slug IN ('demo-a','demo-b') AND v.published_at IS NULL ON CONFLICT DO NOTHING;
INSERT INTO resources(organization_id,id,kind,identifier,display_name)
SELECT id,id,'application','code.exe','Редактор кода' FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO classification_rules(organization_id,id,policy_version_id,resource_id,category)
SELECT o.id,o.id,o.id,o.id,'productive' FROM organizations o
JOIN policy_versions v ON v.organization_id=o.id AND v.id=o.id
WHERE o.slug IN ('demo-a','demo-b') AND v.published_at IS NULL ON CONFLICT DO NOTHING;
UPDATE policy_versions SET published_at=now() WHERE published_at IS NULL AND organization_id IN
('10000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001');
INSERT INTO policy_assignments(organization_id,id,policy_version_id,scope,valid_from)
SELECT id,id,id,'organization','2026-01-01T00:00:00Z' FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO work_sessions(organization_id,id,employee_id,department_id,policy_version_id,started_at,ended_at,end_reason)
SELECT id,id,id,id,id,'2026-10-12T06:00:00Z','2026-10-12T15:00:00Z','schedule'
FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO session_breaks(organization_id,id,session_id,kind,source,started_at,ended_at)
SELECT id,id,id,'lunch','manual','2026-10-12T09:00:00Z','2026-10-12T10:00:00Z'
FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO activity_events(organization_id,id,employee_id,device_id,session_id,client_event_id,event_type,occurred_at,resource_id)
SELECT id,id,id,id,id,id,'focus','2026-10-12T06:01:00Z',id
FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO analytics_runs(organization_id,id,period_start,period_end,algorithm_version,status)
SELECT id,id,'2026-10-12T06:00:00Z','2026-10-12T15:00:00Z','demo-not-production','pending'
FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO activity_intervals(organization_id,id,run_id,session_id,category,started_at,ended_at)
SELECT id,id,id,id,'productive','2026-10-12T06:00:00Z','2026-10-12T09:00:00Z'
FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
INSERT INTO interval_sources(organization_id,interval_id,event_id)
SELECT id,id,id FROM organizations WHERE slug IN ('demo-a','demo-b') ON CONFLICT DO NOTHING;
COMMIT;
