# Физический словарь данных

Автоматический снимок PostgreSQL после V007; источник структуры — SQL-миграции.

Бизнес-таблиц: 27; с включённым и принудительным RLS: 26.

## activity_events

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `employee_id` | `uuid` | NO | `—` |
| `device_id` | `uuid` | NO | `—` |
| `session_id` | `uuid` | YES | `—` |
| `client_event_id` | `uuid` | NO | `—` |
| `event_type` | `text` | NO | `—` |
| `occurred_at` | `timestamp with time zone` | NO | `—` |
| `received_at` | `timestamp with time zone` | NO | `now()` |
| `resource_id` | `uuid` | YES | `—` |

Ограничения:

- `activity_events_client_event_id_not_null`: `NOT NULL client_event_id`
- `activity_events_device_id_not_null`: `NOT NULL device_id`
- `activity_events_employee_id_not_null`: `NOT NULL employee_id`
- `activity_events_event_type_check`: `CHECK ((event_type = ANY (ARRAY['input'::text, 'focus'::text, 'idle'::text, 'login'::text, 'logout'::text, 'heartbeat'::text])))`
- `activity_events_event_type_not_null`: `NOT NULL event_type`
- `activity_events_id_not_null`: `NOT NULL id`
- `activity_events_occurred_at_not_null`: `NOT NULL occurred_at`
- `activity_events_organization_id_device_id_client_event_id_key`: `UNIQUE (organization_id, device_id, client_event_id)`
- `activity_events_organization_id_employee_id_device_id_fkey`: `FOREIGN KEY (organization_id, employee_id, device_id) REFERENCES workpulse.devices(organization_id, employee_id, id)`
- `activity_events_organization_id_employee_id_session_id_fkey`: `FOREIGN KEY (organization_id, employee_id, session_id) REFERENCES workpulse.work_sessions(organization_id, employee_id, id)`
- `activity_events_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `activity_events_organization_id_not_null`: `NOT NULL organization_id`
- `activity_events_organization_id_resource_id_fkey`: `FOREIGN KEY (organization_id, resource_id) REFERENCES workpulse.resources(organization_id, id)`
- `activity_events_pkey`: `PRIMARY KEY (organization_id, id)`
- `activity_events_received_at_not_null`: `NOT NULL received_at`

## activity_intervals

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `run_id` | `uuid` | NO | `—` |
| `session_id` | `uuid` | NO | `—` |
| `category` | `text` | NO | `—` |
| `started_at` | `timestamp with time zone` | NO | `—` |
| `ended_at` | `timestamp with time zone` | NO | `—` |

Ограничения:

- `activity_intervals_category_check`: `CHECK ((category = ANY (ARRAY['productive'::text, 'non_target'::text, 'neutral'::text, 'idle'::text, 'lunch'::text, 'authorized_absence'::text, 'unknown'::text])))`
- `activity_intervals_category_not_null`: `NOT NULL category`
- `activity_intervals_check`: `CHECK ((ended_at > started_at))`
- `activity_intervals_ended_at_not_null`: `NOT NULL ended_at`
- `activity_intervals_id_not_null`: `NOT NULL id`
- `activity_intervals_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `activity_intervals_organization_id_not_null`: `NOT NULL organization_id`
- `activity_intervals_organization_id_run_id_fkey`: `FOREIGN KEY (organization_id, run_id) REFERENCES workpulse.analytics_runs(organization_id, id)`
- `activity_intervals_organization_id_run_id_session_id_tstzr_excl`: `EXCLUDE USING gist (organization_id WITH =, run_id WITH =, session_id WITH =, tstzrange(started_at, ended_at, '[)'::text) WITH &&)`
- `activity_intervals_organization_id_session_id_fkey`: `FOREIGN KEY (organization_id, session_id) REFERENCES workpulse.work_sessions(organization_id, id)`
- `activity_intervals_pkey`: `PRIMARY KEY (organization_id, id)`
- `activity_intervals_run_id_not_null`: `NOT NULL run_id`
- `activity_intervals_session_id_not_null`: `NOT NULL session_id`
- `activity_intervals_started_at_not_null`: `NOT NULL started_at`

## analytics_runs

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `period_start` | `timestamp with time zone` | NO | `—` |
| `period_end` | `timestamp with time zone` | NO | `—` |
| `algorithm_version` | `text` | NO | `—` |
| `status` | `text` | NO | `—` |
| `created_at` | `timestamp with time zone` | NO | `now()` |
| `completed_at` | `timestamp with time zone` | YES | `—` |

Ограничения:

- `analytics_runs_algorithm_version_check`: `CHECK ((btrim(algorithm_version) <> ''::text))`
- `analytics_runs_algorithm_version_not_null`: `NOT NULL algorithm_version`
- `analytics_runs_check`: `CHECK ((period_end > period_start))`
- `analytics_runs_check1`: `CHECK (((status = ANY (ARRAY['completed'::text, 'failed'::text])) = (completed_at IS NOT NULL)))`
- `analytics_runs_check2`: `CHECK ((completed_at >= created_at))`
- `analytics_runs_created_at_not_null`: `NOT NULL created_at`
- `analytics_runs_id_not_null`: `NOT NULL id`
- `analytics_runs_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `analytics_runs_organization_id_not_null`: `NOT NULL organization_id`
- `analytics_runs_period_end_not_null`: `NOT NULL period_end`
- `analytics_runs_period_start_not_null`: `NOT NULL period_start`
- `analytics_runs_pkey`: `PRIMARY KEY (organization_id, id)`
- `analytics_runs_status_check`: `CHECK ((status = ANY (ARRAY['pending'::text, 'running'::text, 'completed'::text, 'failed'::text])))`
- `analytics_runs_status_not_null`: `NOT NULL status`

## audit_log

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `actor_id` | `uuid` | YES | `—` |
| `table_name` | `text` | NO | `—` |
| `operation` | `text` | NO | `—` |
| `entity_id` | `uuid` | NO | `—` |
| `occurred_at` | `timestamp with time zone` | NO | `now()` |
| `before_data` | `jsonb` | YES | `—` |
| `after_data` | `jsonb` | YES | `—` |

Ограничения:

- `audit_log_entity_id_not_null`: `NOT NULL entity_id`
- `audit_log_id_not_null`: `NOT NULL id`
- `audit_log_occurred_at_not_null`: `NOT NULL occurred_at`
- `audit_log_operation_check`: `CHECK ((operation = ANY (ARRAY['INSERT'::text, 'UPDATE'::text, 'DELETE'::text])))`
- `audit_log_operation_not_null`: `NOT NULL operation`
- `audit_log_organization_id_actor_id_fkey`: `FOREIGN KEY (organization_id, actor_id) REFERENCES workpulse.user_accounts(organization_id, id)`
- `audit_log_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `audit_log_organization_id_not_null`: `NOT NULL organization_id`
- `audit_log_pkey`: `PRIMARY KEY (organization_id, id)`
- `audit_log_table_name_not_null`: `NOT NULL table_name`

## classification_rules

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `policy_version_id` | `uuid` | NO | `—` |
| `resource_id` | `uuid` | NO | `—` |
| `category` | `text` | NO | `—` |

Ограничения:

- `classification_rules_category_check`: `CHECK ((category = ANY (ARRAY['productive'::text, 'non_target'::text, 'neutral'::text])))`
- `classification_rules_category_not_null`: `NOT NULL category`
- `classification_rules_id_not_null`: `NOT NULL id`
- `classification_rules_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `classification_rules_organization_id_not_null`: `NOT NULL organization_id`
- `classification_rules_organization_id_policy_version_id_fkey`: `FOREIGN KEY (organization_id, policy_version_id) REFERENCES workpulse.policy_versions(organization_id, id)`
- `classification_rules_organization_id_policy_version_id_reso_key`: `UNIQUE (organization_id, policy_version_id, resource_id)`
- `classification_rules_organization_id_resource_id_fkey`: `FOREIGN KEY (organization_id, resource_id) REFERENCES workpulse.resources(organization_id, id)`
- `classification_rules_pkey`: `PRIMARY KEY (organization_id, id)`
- `classification_rules_policy_version_id_not_null`: `NOT NULL policy_version_id`
- `classification_rules_resource_id_not_null`: `NOT NULL resource_id`

## departments

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `code` | `text` | NO | `—` |
| `name` | `text` | NO | `—` |

Ограничения:

- `departments_code_check`: `CHECK ((btrim(code) <> ''::text))`
- `departments_code_not_null`: `NOT NULL code`
- `departments_id_not_null`: `NOT NULL id`
- `departments_name_check`: `CHECK ((btrim(name) <> ''::text))`
- `departments_name_not_null`: `NOT NULL name`
- `departments_organization_id_code_key`: `UNIQUE (organization_id, code)`
- `departments_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `departments_organization_id_not_null`: `NOT NULL organization_id`
- `departments_pkey`: `PRIMARY KEY (organization_id, id)`

## devices

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `employee_id` | `uuid` | NO | `—` |
| `name` | `text` | NO | `—` |
| `platform` | `text` | NO | `—` |
| `registered_at` | `timestamp with time zone` | NO | `now()` |
| `retired_at` | `timestamp with time zone` | YES | `—` |

Ограничения:

- `devices_check`: `CHECK ((retired_at >= registered_at))`
- `devices_employee_id_not_null`: `NOT NULL employee_id`
- `devices_id_not_null`: `NOT NULL id`
- `devices_name_not_null`: `NOT NULL name`
- `devices_organization_id_employee_id_fkey`: `FOREIGN KEY (organization_id, employee_id) REFERENCES workpulse.employees(organization_id, id)`
- `devices_organization_id_employee_id_id_key`: `UNIQUE (organization_id, employee_id, id)`
- `devices_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `devices_organization_id_not_null`: `NOT NULL organization_id`
- `devices_pkey`: `PRIMARY KEY (organization_id, id)`
- `devices_platform_check`: `CHECK ((platform = ANY (ARRAY['windows'::text, 'linux'::text, 'macos'::text])))`
- `devices_platform_not_null`: `NOT NULL platform`
- `devices_registered_at_not_null`: `NOT NULL registered_at`

## employee_external_ids

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `connection_id` | `uuid` | NO | `—` |
| `employee_id` | `uuid` | NO | `—` |
| `external_id` | `text` | NO | `—` |

Ограничения:

- `employee_external_ids_connection_id_not_null`: `NOT NULL connection_id`
- `employee_external_ids_employee_id_not_null`: `NOT NULL employee_id`
- `employee_external_ids_external_id_check`: `CHECK ((btrim(external_id) <> ''::text))`
- `employee_external_ids_external_id_not_null`: `NOT NULL external_id`
- `employee_external_ids_organization_id_connection_id_externa_key`: `UNIQUE (organization_id, connection_id, external_id)`
- `employee_external_ids_organization_id_connection_id_fkey`: `FOREIGN KEY (organization_id, connection_id) REFERENCES workpulse.integration_connections(organization_id, id)`
- `employee_external_ids_organization_id_employee_id_fkey`: `FOREIGN KEY (organization_id, employee_id) REFERENCES workpulse.employees(organization_id, id)`
- `employee_external_ids_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `employee_external_ids_organization_id_not_null`: `NOT NULL organization_id`
- `employee_external_ids_pkey`: `PRIMARY KEY (organization_id, connection_id, employee_id)`

## employees

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `department_id` | `uuid` | NO | `—` |
| `employee_number` | `text` | NO | `—` |
| `full_name` | `text` | NO | `—` |
| `position_name` | `text` | YES | `—` |
| `hired_on` | `date` | NO | `—` |
| `terminated_on` | `date` | YES | `—` |

Ограничения:

- `employees_check`: `CHECK ((terminated_on >= hired_on))`
- `employees_department_id_not_null`: `NOT NULL department_id`
- `employees_employee_number_check`: `CHECK ((btrim(employee_number) <> ''::text))`
- `employees_employee_number_not_null`: `NOT NULL employee_number`
- `employees_full_name_check`: `CHECK ((btrim(full_name) <> ''::text))`
- `employees_full_name_not_null`: `NOT NULL full_name`
- `employees_hired_on_not_null`: `NOT NULL hired_on`
- `employees_id_not_null`: `NOT NULL id`
- `employees_organization_id_department_id_fkey`: `FOREIGN KEY (organization_id, department_id) REFERENCES workpulse.departments(organization_id, id)`
- `employees_organization_id_employee_number_key`: `UNIQUE (organization_id, employee_number)`
- `employees_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `employees_organization_id_not_null`: `NOT NULL organization_id`
- `employees_pkey`: `PRIMARY KEY (organization_id, id)`

## integration_connections

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `provider` | `text` | NO | `—` |
| `name` | `text` | NO | `—` |
| `secret_reference` | `text` | YES | `—` |
| `enabled` | `boolean` | NO | `false` |

Ограничения:

- `integration_connections_enabled_not_null`: `NOT NULL enabled`
- `integration_connections_id_not_null`: `NOT NULL id`
- `integration_connections_name_not_null`: `NOT NULL name`
- `integration_connections_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `integration_connections_organization_id_name_key`: `UNIQUE (organization_id, name)`
- `integration_connections_organization_id_not_null`: `NOT NULL organization_id`
- `integration_connections_pkey`: `PRIMARY KEY (organization_id, id)`
- `integration_connections_provider_check`: `CHECK ((btrim(provider) <> ''::text))`
- `integration_connections_provider_not_null`: `NOT NULL provider`

## integration_jobs

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `connection_id` | `uuid` | NO | `—` |
| `status` | `text` | NO | `'pending'::text` |
| `created_at` | `timestamp with time zone` | NO | `now()` |
| `finished_at` | `timestamp with time zone` | YES | `—` |

Ограничения:

- `integration_jobs_check`: `CHECK ((finished_at >= created_at))`
- `integration_jobs_check1`: `CHECK (((status = ANY (ARRAY['completed'::text, 'failed'::text])) = (finished_at IS NOT NULL)))`
- `integration_jobs_connection_id_not_null`: `NOT NULL connection_id`
- `integration_jobs_created_at_not_null`: `NOT NULL created_at`
- `integration_jobs_id_not_null`: `NOT NULL id`
- `integration_jobs_organization_id_connection_id_fkey`: `FOREIGN KEY (organization_id, connection_id) REFERENCES workpulse.integration_connections(organization_id, id)`
- `integration_jobs_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `integration_jobs_organization_id_not_null`: `NOT NULL organization_id`
- `integration_jobs_pkey`: `PRIMARY KEY (organization_id, id)`
- `integration_jobs_status_check`: `CHECK ((status = ANY (ARRAY['pending'::text, 'running'::text, 'completed'::text, 'failed'::text])))`
- `integration_jobs_status_not_null`: `NOT NULL status`

## interval_sources

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `interval_id` | `uuid` | NO | `—` |
| `event_id` | `uuid` | NO | `—` |

Ограничения:

- `interval_sources_event_id_not_null`: `NOT NULL event_id`
- `interval_sources_interval_id_not_null`: `NOT NULL interval_id`
- `interval_sources_organization_id_event_id_fkey`: `FOREIGN KEY (organization_id, event_id) REFERENCES workpulse.activity_events(organization_id, id)`
- `interval_sources_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `interval_sources_organization_id_interval_id_fkey`: `FOREIGN KEY (organization_id, interval_id) REFERENCES workpulse.activity_intervals(organization_id, id)`
- `interval_sources_organization_id_not_null`: `NOT NULL organization_id`
- `interval_sources_pkey`: `PRIMARY KEY (organization_id, interval_id, event_id)`

## monitoring_policies

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `name` | `text` | NO | `—` |
| `archived` | `boolean` | NO | `false` |

Ограничения:

- `monitoring_policies_archived_not_null`: `NOT NULL archived`
- `monitoring_policies_id_not_null`: `NOT NULL id`
- `monitoring_policies_name_check`: `CHECK ((btrim(name) <> ''::text))`
- `monitoring_policies_name_not_null`: `NOT NULL name`
- `monitoring_policies_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `monitoring_policies_organization_id_name_key`: `UNIQUE (organization_id, name)`
- `monitoring_policies_organization_id_not_null`: `NOT NULL organization_id`
- `monitoring_policies_pkey`: `PRIMARY KEY (organization_id, id)`

## notifications

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `recipient_id` | `uuid` | NO | `—` |
| `violation_id` | `uuid` | YES | `—` |
| `channel` | `text` | NO | `—` |
| `status` | `text` | NO | `'pending'::text` |
| `created_at` | `timestamp with time zone` | NO | `now()` |
| `delivered_at` | `timestamp with time zone` | YES | `—` |

Ограничения:

- `notifications_channel_check`: `CHECK ((channel = ANY (ARRAY['in_app'::text, 'email'::text])))`
- `notifications_channel_not_null`: `NOT NULL channel`
- `notifications_check`: `CHECK ((delivered_at >= created_at))`
- `notifications_created_at_not_null`: `NOT NULL created_at`
- `notifications_id_not_null`: `NOT NULL id`
- `notifications_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `notifications_organization_id_not_null`: `NOT NULL organization_id`
- `notifications_organization_id_recipient_id_fkey`: `FOREIGN KEY (organization_id, recipient_id) REFERENCES workpulse.user_accounts(organization_id, id)`
- `notifications_organization_id_violation_id_fkey`: `FOREIGN KEY (organization_id, violation_id) REFERENCES workpulse.violations(organization_id, id)`
- `notifications_pkey`: `PRIMARY KEY (organization_id, id)`
- `notifications_recipient_id_not_null`: `NOT NULL recipient_id`
- `notifications_status_check`: `CHECK ((status = ANY (ARRAY['pending'::text, 'sent'::text, 'failed'::text, 'read'::text])))`
- `notifications_status_not_null`: `NOT NULL status`

## organizations

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `slug` | `text` | NO | `—` |
| `name` | `text` | NO | `—` |
| `time_zone` | `text` | NO | `'UTC'::text` |
| `retention_days` | `integer` | YES | `—` |
| `created_at` | `timestamp with time zone` | NO | `now()` |

Ограничения:

- `organizations_created_at_not_null`: `NOT NULL created_at`
- `organizations_id_not_null`: `NOT NULL id`
- `organizations_name_check`: `CHECK ((btrim(name) <> ''::text))`
- `organizations_name_not_null`: `NOT NULL name`
- `organizations_pkey`: `PRIMARY KEY (id)`
- `organizations_retention_days_check`: `CHECK ((retention_days > 0))`
- `organizations_slug_check`: `CHECK ((slug ~ '^[a-z0-9][a-z0-9-]{1,62}$'::text))`
- `organizations_slug_key`: `UNIQUE (slug)`
- `organizations_slug_not_null`: `NOT NULL slug`
- `organizations_time_zone_not_null`: `NOT NULL time_zone`

## policy_assignments

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `policy_version_id` | `uuid` | NO | `—` |
| `scope` | `text` | NO | `—` |
| `department_id` | `uuid` | YES | `—` |
| `employee_id` | `uuid` | YES | `—` |
| `valid_from` | `timestamp with time zone` | NO | `—` |
| `valid_until` | `timestamp with time zone` | YES | `—` |

Ограничения:

- `policy_assignments_check`: `CHECK ((valid_until > valid_from))`
- `policy_assignments_check1`: `CHECK ((((scope = 'organization'::text) AND (department_id IS NULL) AND (employee_id IS NULL)) OR ((scope = 'department'::text) AND (department_id IS NOT NULL) AND (employee_id IS NULL)) OR ((scope = 'employee'::text) AND (employee_id IS NOT NULL) AND (department_id IS NULL))))`
- `policy_assignments_id_not_null`: `NOT NULL id`
- `policy_assignments_organization_id_department_id_fkey`: `FOREIGN KEY (organization_id, department_id) REFERENCES workpulse.departments(organization_id, id)`
- `policy_assignments_organization_id_employee_id_fkey`: `FOREIGN KEY (organization_id, employee_id) REFERENCES workpulse.employees(organization_id, id)`
- `policy_assignments_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `policy_assignments_organization_id_not_null`: `NOT NULL organization_id`
- `policy_assignments_organization_id_policy_version_id_fkey`: `FOREIGN KEY (organization_id, policy_version_id) REFERENCES workpulse.policy_versions(organization_id, id)`
- `policy_assignments_organization_id_scope_coalesce_tstzrang_excl`: `EXCLUDE USING gist (organization_id WITH =, scope WITH =, COALESCE(department_id, employee_id, organization_id) WITH =, tstzrange(valid_from, valid_until, '[)'::text) WITH &&)`
- `policy_assignments_pkey`: `PRIMARY KEY (organization_id, id)`
- `policy_assignments_policy_version_id_not_null`: `NOT NULL policy_version_id`
- `policy_assignments_scope_check`: `CHECK ((scope = ANY (ARRAY['organization'::text, 'department'::text, 'employee'::text])))`
- `policy_assignments_scope_not_null`: `NOT NULL scope`
- `policy_assignments_valid_from_not_null`: `NOT NULL valid_from`

## policy_versions

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `policy_id` | `uuid` | NO | `—` |
| `version_number` | `integer` | NO | `—` |
| `idle_limit_seconds` | `integer` | NO | `—` |
| `non_target_limit_seconds` | `integer` | NO | `—` |
| `lunch_limit_seconds` | `integer` | NO | `—` |
| `time_zone` | `text` | NO | `—` |
| `published_at` | `timestamp with time zone` | YES | `—` |

Ограничения:

- `policy_versions_id_not_null`: `NOT NULL id`
- `policy_versions_idle_limit_seconds_check`: `CHECK ((idle_limit_seconds > 0))`
- `policy_versions_idle_limit_seconds_not_null`: `NOT NULL idle_limit_seconds`
- `policy_versions_lunch_limit_seconds_check`: `CHECK ((lunch_limit_seconds > 0))`
- `policy_versions_lunch_limit_seconds_not_null`: `NOT NULL lunch_limit_seconds`
- `policy_versions_non_target_limit_seconds_check`: `CHECK ((non_target_limit_seconds > 0))`
- `policy_versions_non_target_limit_seconds_not_null`: `NOT NULL non_target_limit_seconds`
- `policy_versions_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `policy_versions_organization_id_not_null`: `NOT NULL organization_id`
- `policy_versions_organization_id_policy_id_fkey`: `FOREIGN KEY (organization_id, policy_id) REFERENCES workpulse.monitoring_policies(organization_id, id)`
- `policy_versions_organization_id_policy_id_version_number_key`: `UNIQUE (organization_id, policy_id, version_number)`
- `policy_versions_pkey`: `PRIMARY KEY (organization_id, id)`
- `policy_versions_policy_id_not_null`: `NOT NULL policy_id`
- `policy_versions_time_zone_not_null`: `NOT NULL time_zone`
- `policy_versions_version_number_check`: `CHECK ((version_number > 0))`
- `policy_versions_version_number_not_null`: `NOT NULL version_number`

## report_exports

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `requested_by` | `uuid` | NO | `—` |
| `run_id` | `uuid` | NO | `—` |
| `format` | `text` | NO | `—` |
| `status` | `text` | NO | `'pending'::text` |
| `artifact_reference` | `text` | YES | `—` |
| `created_at` | `timestamp with time zone` | NO | `now()` |

Ограничения:

- `report_exports_check`: `CHECK (((status <> 'completed'::text) OR (artifact_reference IS NOT NULL)))`
- `report_exports_created_at_not_null`: `NOT NULL created_at`
- `report_exports_format_check`: `CHECK ((format = ANY (ARRAY['pdf'::text, 'xlsx'::text])))`
- `report_exports_format_not_null`: `NOT NULL format`
- `report_exports_id_not_null`: `NOT NULL id`
- `report_exports_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `report_exports_organization_id_not_null`: `NOT NULL organization_id`
- `report_exports_organization_id_requested_by_fkey`: `FOREIGN KEY (organization_id, requested_by) REFERENCES workpulse.user_accounts(organization_id, id)`
- `report_exports_organization_id_run_id_fkey`: `FOREIGN KEY (organization_id, run_id) REFERENCES workpulse.analytics_runs(organization_id, id)`
- `report_exports_pkey`: `PRIMARY KEY (organization_id, id)`
- `report_exports_requested_by_not_null`: `NOT NULL requested_by`
- `report_exports_run_id_not_null`: `NOT NULL run_id`
- `report_exports_status_check`: `CHECK ((status = ANY (ARRAY['pending'::text, 'running'::text, 'completed'::text, 'failed'::text])))`
- `report_exports_status_not_null`: `NOT NULL status`

## resources

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `kind` | `text` | NO | `—` |
| `identifier` | `text` | NO | `—` |
| `display_name` | `text` | NO | `—` |

Ограничения:

- `resources_check`: `CHECK (((kind <> 'domain'::text) OR (identifier ~ '^[a-z0-9.-]+$'::text)))`
- `resources_display_name_not_null`: `NOT NULL display_name`
- `resources_id_not_null`: `NOT NULL id`
- `resources_identifier_check`: `CHECK (((identifier = lower(identifier)) AND (btrim(identifier) <> ''::text)))`
- `resources_identifier_not_null`: `NOT NULL identifier`
- `resources_kind_check`: `CHECK ((kind = ANY (ARRAY['application'::text, 'domain'::text])))`
- `resources_kind_not_null`: `NOT NULL kind`
- `resources_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `resources_organization_id_kind_identifier_key`: `UNIQUE (organization_id, kind, identifier)`
- `resources_organization_id_not_null`: `NOT NULL organization_id`
- `resources_pkey`: `PRIMARY KEY (organization_id, id)`

## role_assignments

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `user_id` | `uuid` | NO | `—` |
| `role_code` | `text` | NO | `—` |
| `scope` | `text` | NO | `—` |
| `department_id` | `uuid` | YES | `—` |

Ограничения:

- `role_assignments_check`: `CHECK (((scope = 'department'::text) = (department_id IS NOT NULL)))`
- `role_assignments_id_not_null`: `NOT NULL id`
- `role_assignments_organization_id_department_id_fkey`: `FOREIGN KEY (organization_id, department_id) REFERENCES workpulse.departments(organization_id, id)`
- `role_assignments_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `role_assignments_organization_id_not_null`: `NOT NULL organization_id`
- `role_assignments_organization_id_user_id_fkey`: `FOREIGN KEY (organization_id, user_id) REFERENCES workpulse.user_accounts(organization_id, id)`
- `role_assignments_organization_id_user_id_role_code_scope_de_key`: `UNIQUE NULLS NOT DISTINCT (organization_id, user_id, role_code, scope, department_id)`
- `role_assignments_pkey`: `PRIMARY KEY (organization_id, id)`
- `role_assignments_role_code_fkey`: `FOREIGN KEY (role_code) REFERENCES workpulse.roles(code)`
- `role_assignments_role_code_not_null`: `NOT NULL role_code`
- `role_assignments_scope_check`: `CHECK ((scope = ANY (ARRAY['organization'::text, 'department'::text, 'self'::text])))`
- `role_assignments_scope_not_null`: `NOT NULL scope`
- `role_assignments_user_id_not_null`: `NOT NULL user_id`

## roles

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `code` | `text` | NO | `—` |
| `name` | `text` | NO | `—` |

Ограничения:

- `roles_code_not_null`: `NOT NULL code`
- `roles_name_not_null`: `NOT NULL name`
- `roles_pkey`: `PRIMARY KEY (code)`

## schedule_days

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `policy_version_id` | `uuid` | NO | `—` |
| `iso_weekday` | `smallint` | NO | `—` |
| `starts_at` | `time without time zone` | NO | `—` |
| `ends_at` | `time without time zone` | NO | `—` |
| `ends_next_day` | `boolean` | NO | `false` |

Ограничения:

- `schedule_days_check`: `CHECK ((((NOT ends_next_day) AND (ends_at > starts_at)) OR (ends_next_day AND (ends_at <= starts_at))))`
- `schedule_days_ends_at_not_null`: `NOT NULL ends_at`
- `schedule_days_ends_next_day_not_null`: `NOT NULL ends_next_day`
- `schedule_days_id_not_null`: `NOT NULL id`
- `schedule_days_iso_weekday_check`: `CHECK (((iso_weekday >= 1) AND (iso_weekday <= 7)))`
- `schedule_days_iso_weekday_not_null`: `NOT NULL iso_weekday`
- `schedule_days_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `schedule_days_organization_id_not_null`: `NOT NULL organization_id`
- `schedule_days_organization_id_policy_version_id_fkey`: `FOREIGN KEY (organization_id, policy_version_id) REFERENCES workpulse.policy_versions(organization_id, id)`
- `schedule_days_organization_id_policy_version_id_iso_weekday_key`: `UNIQUE (organization_id, policy_version_id, iso_weekday)`
- `schedule_days_pkey`: `PRIMARY KEY (organization_id, id)`
- `schedule_days_policy_version_id_not_null`: `NOT NULL policy_version_id`
- `schedule_days_starts_at_not_null`: `NOT NULL starts_at`

## session_breaks

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `session_id` | `uuid` | NO | `—` |
| `kind` | `text` | NO | `—` |
| `source` | `text` | NO | `—` |
| `started_at` | `timestamp with time zone` | NO | `—` |
| `ended_at` | `timestamp with time zone` | YES | `—` |

Ограничения:

- `session_breaks_check`: `CHECK ((ended_at > started_at))`
- `session_breaks_id_not_null`: `NOT NULL id`
- `session_breaks_kind_check`: `CHECK ((kind = ANY (ARRAY['lunch'::text, 'authorized_absence'::text, 'rest'::text])))`
- `session_breaks_kind_not_null`: `NOT NULL kind`
- `session_breaks_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `session_breaks_organization_id_not_null`: `NOT NULL organization_id`
- `session_breaks_organization_id_session_id_fkey`: `FOREIGN KEY (organization_id, session_id) REFERENCES workpulse.work_sessions(organization_id, id)`
- `session_breaks_organization_id_session_id_tstzrange_excl`: `EXCLUDE USING gist (organization_id WITH =, session_id WITH =, tstzrange(started_at, ended_at, '[)'::text) WITH &&)`
- `session_breaks_pkey`: `PRIMARY KEY (organization_id, id)`
- `session_breaks_session_id_not_null`: `NOT NULL session_id`
- `session_breaks_source_check`: `CHECK ((source = ANY (ARRAY['manual'::text, 'schedule'::text, 'agent'::text])))`
- `session_breaks_source_not_null`: `NOT NULL source`
- `session_breaks_started_at_not_null`: `NOT NULL started_at`

## user_accounts

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `employee_id` | `uuid` | YES | `—` |
| `email` | `text` | NO | `—` |
| `is_active` | `boolean` | NO | `true` |
| `external_subject` | `text` | YES | `—` |

Ограничения:

- `user_accounts_email_check`: `CHECK (((email = lower(email)) AND (email ~ '^[^ @]+@[^ @]+\.[^ @]+$'::text)))`
- `user_accounts_email_not_null`: `NOT NULL email`
- `user_accounts_id_not_null`: `NOT NULL id`
- `user_accounts_is_active_not_null`: `NOT NULL is_active`
- `user_accounts_organization_id_email_key`: `UNIQUE (organization_id, email)`
- `user_accounts_organization_id_employee_id_fkey`: `FOREIGN KEY (organization_id, employee_id) REFERENCES workpulse.employees(organization_id, id)`
- `user_accounts_organization_id_employee_id_key`: `UNIQUE (organization_id, employee_id)`
- `user_accounts_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `user_accounts_organization_id_not_null`: `NOT NULL organization_id`
- `user_accounts_pkey`: `PRIMARY KEY (organization_id, id)`

## violation_comments

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `violation_id` | `uuid` | NO | `—` |
| `author_id` | `uuid` | NO | `—` |
| `body` | `text` | NO | `—` |
| `created_at` | `timestamp with time zone` | NO | `now()` |

Ограничения:

- `violation_comments_author_id_not_null`: `NOT NULL author_id`
- `violation_comments_body_check`: `CHECK (((length(btrim(body)) >= 1) AND (length(btrim(body)) <= 4000)))`
- `violation_comments_body_not_null`: `NOT NULL body`
- `violation_comments_created_at_not_null`: `NOT NULL created_at`
- `violation_comments_id_not_null`: `NOT NULL id`
- `violation_comments_organization_id_author_id_fkey`: `FOREIGN KEY (organization_id, author_id) REFERENCES workpulse.user_accounts(organization_id, id)`
- `violation_comments_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `violation_comments_organization_id_not_null`: `NOT NULL organization_id`
- `violation_comments_organization_id_violation_id_fkey`: `FOREIGN KEY (organization_id, violation_id) REFERENCES workpulse.violations(organization_id, id)`
- `violation_comments_pkey`: `PRIMARY KEY (organization_id, id)`
- `violation_comments_violation_id_not_null`: `NOT NULL violation_id`

## violations

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `employee_id` | `uuid` | NO | `—` |
| `session_id` | `uuid` | NO | `—` |
| `kind` | `text` | NO | `—` |
| `detected_at` | `timestamp with time zone` | NO | `now()` |
| `started_at` | `timestamp with time zone` | NO | `—` |
| `ended_at` | `timestamp with time zone` | YES | `—` |
| `status` | `text` | NO | `'new'::text` |
| `deduplication_key` | `text` | NO | `—` |
| `reviewed_by` | `uuid` | YES | `—` |

Ограничения:

- `violations_check`: `CHECK ((ended_at >= started_at))`
- `violations_deduplication_key_check`: `CHECK ((btrim(deduplication_key) <> ''::text))`
- `violations_deduplication_key_not_null`: `NOT NULL deduplication_key`
- `violations_detected_at_not_null`: `NOT NULL detected_at`
- `violations_employee_id_not_null`: `NOT NULL employee_id`
- `violations_id_not_null`: `NOT NULL id`
- `violations_kind_check`: `CHECK ((kind = ANY (ARRAY['idle'::text, 'non_target'::text, 'late_arrival'::text, 'early_departure'::text, 'lunch_overrun'::text, 'break_overrun'::text])))`
- `violations_kind_not_null`: `NOT NULL kind`
- `violations_organization_id_deduplication_key_key`: `UNIQUE (organization_id, deduplication_key)`
- `violations_organization_id_employee_id_session_id_fkey`: `FOREIGN KEY (organization_id, employee_id, session_id) REFERENCES workpulse.work_sessions(organization_id, employee_id, id)`
- `violations_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `violations_organization_id_not_null`: `NOT NULL organization_id`
- `violations_organization_id_reviewed_by_fkey`: `FOREIGN KEY (organization_id, reviewed_by) REFERENCES workpulse.user_accounts(organization_id, id)`
- `violations_pkey`: `PRIMARY KEY (organization_id, id)`
- `violations_session_id_not_null`: `NOT NULL session_id`
- `violations_started_at_not_null`: `NOT NULL started_at`
- `violations_status_check`: `CHECK ((status = ANY (ARRAY['new'::text, 'reviewed'::text, 'dismissed'::text])))`
- `violations_status_not_null`: `NOT NULL status`

## work_sessions

| Поле | Тип PostgreSQL | NULL | Значение по умолчанию |
|---|---|---|---|
| `organization_id` | `uuid` | NO | `—` |
| `id` | `uuid` | NO | `gen_random_uuid()` |
| `employee_id` | `uuid` | NO | `—` |
| `department_id` | `uuid` | NO | `—` |
| `policy_version_id` | `uuid` | NO | `—` |
| `started_at` | `timestamp with time zone` | NO | `—` |
| `ended_at` | `timestamp with time zone` | YES | `—` |
| `end_reason` | `text` | YES | `—` |

Ограничения:

- `work_sessions_check`: `CHECK ((ended_at > started_at))`
- `work_sessions_check1`: `CHECK (((ended_at IS NULL) = (end_reason IS NULL)))`
- `work_sessions_department_id_not_null`: `NOT NULL department_id`
- `work_sessions_employee_id_not_null`: `NOT NULL employee_id`
- `work_sessions_end_reason_check`: `CHECK ((end_reason = ANY (ARRAY['logout'::text, 'inactivity'::text, 'schedule'::text, 'manual'::text])))`
- `work_sessions_id_not_null`: `NOT NULL id`
- `work_sessions_organization_id_department_id_fkey`: `FOREIGN KEY (organization_id, department_id) REFERENCES workpulse.departments(organization_id, id)`
- `work_sessions_organization_id_employee_id_fkey`: `FOREIGN KEY (organization_id, employee_id) REFERENCES workpulse.employees(organization_id, id)`
- `work_sessions_organization_id_employee_id_id_key`: `UNIQUE (organization_id, employee_id, id)`
- `work_sessions_organization_id_employee_id_tstzrange_excl`: `EXCLUDE USING gist (organization_id WITH =, employee_id WITH =, tstzrange(started_at, ended_at, '[)'::text) WITH &&)`
- `work_sessions_organization_id_fkey`: `FOREIGN KEY (organization_id) REFERENCES workpulse.organizations(id)`
- `work_sessions_organization_id_not_null`: `NOT NULL organization_id`
- `work_sessions_organization_id_policy_version_id_fkey`: `FOREIGN KEY (organization_id, policy_version_id) REFERENCES workpulse.policy_versions(organization_id, id)`
- `work_sessions_pkey`: `PRIMARY KEY (organization_id, id)`
- `work_sessions_policy_version_id_not_null`: `NOT NULL policy_version_id`
- `work_sessions_started_at_not_null`: `NOT NULL started_at`

