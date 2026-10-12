SET search_path = workpulse, public;
CREATE TABLE violations (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    session_id uuid NOT NULL,
    kind text NOT NULL CHECK (kind IN ('idle','non_target','late_arrival','early_departure','lunch_overrun','break_overrun')),
    detected_at timestamptz NOT NULL DEFAULT now(),
    started_at timestamptz NOT NULL,
    ended_at timestamptz CHECK (ended_at >= started_at),
    status text NOT NULL DEFAULT 'new' CHECK (status IN ('new','reviewed','dismissed')),
    deduplication_key text NOT NULL CHECK (btrim(deduplication_key) <> ''),
    reviewed_by uuid,
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,deduplication_key),
    FOREIGN KEY (organization_id,employee_id,session_id) REFERENCES work_sessions(organization_id,employee_id,id),
    FOREIGN KEY (organization_id,reviewed_by) REFERENCES user_accounts(organization_id,id)
);
CREATE TABLE violation_comments (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    violation_id uuid NOT NULL,
    author_id uuid NOT NULL,
    body text NOT NULL CHECK (length(btrim(body)) BETWEEN 1 AND 4000),
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id,id),
    FOREIGN KEY (organization_id,violation_id) REFERENCES violations(organization_id,id),
    FOREIGN KEY (organization_id,author_id) REFERENCES user_accounts(organization_id,id)
);
CREATE TABLE notifications (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    recipient_id uuid NOT NULL,
    violation_id uuid,
    channel text NOT NULL CHECK (channel IN ('in_app','email')),
    status text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending','sent','failed','read')),
    created_at timestamptz NOT NULL DEFAULT now(),
    delivered_at timestamptz CHECK (delivered_at >= created_at),
    PRIMARY KEY (organization_id,id),
    FOREIGN KEY (organization_id,recipient_id) REFERENCES user_accounts(organization_id,id),
    FOREIGN KEY (organization_id,violation_id) REFERENCES violations(organization_id,id)
);
CREATE TABLE report_exports (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    requested_by uuid NOT NULL,
    run_id uuid NOT NULL,
    format text NOT NULL CHECK (format IN ('pdf','xlsx')),
    status text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending','running','completed','failed')),
    artifact_reference text,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id,id),
    CHECK (status <> 'completed' OR artifact_reference IS NOT NULL),
    FOREIGN KEY (organization_id,requested_by) REFERENCES user_accounts(organization_id,id),
    FOREIGN KEY (organization_id,run_id) REFERENCES analytics_runs(organization_id,id)
);
CREATE TABLE integration_connections (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    provider text NOT NULL CHECK (btrim(provider) <> ''),
    name text NOT NULL,
    secret_reference text,
    enabled boolean NOT NULL DEFAULT false,
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,name)
);
CREATE TABLE employee_external_ids (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    connection_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    external_id text NOT NULL CHECK (btrim(external_id) <> ''),
    PRIMARY KEY (organization_id,connection_id,employee_id), UNIQUE (organization_id,connection_id,external_id),
    FOREIGN KEY (organization_id,connection_id) REFERENCES integration_connections(organization_id,id),
    FOREIGN KEY (organization_id,employee_id) REFERENCES employees(organization_id,id)
);
CREATE TABLE integration_jobs (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    connection_id uuid NOT NULL,
    status text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending','running','completed','failed')),
    created_at timestamptz NOT NULL DEFAULT now(),
    finished_at timestamptz CHECK (finished_at >= created_at),
    PRIMARY KEY (organization_id,id),
    CHECK ((status IN ('completed','failed')) = (finished_at IS NOT NULL)),
    FOREIGN KEY (organization_id,connection_id) REFERENCES integration_connections(organization_id,id)
);
CREATE INDEX violations_employee_time_idx ON violations(organization_id,employee_id,started_at DESC);
CREATE INDEX violations_status_idx ON violations(organization_id,status,detected_at DESC);
CREATE INDEX notification_queue_idx ON notifications(organization_id,created_at) WHERE status='pending';
