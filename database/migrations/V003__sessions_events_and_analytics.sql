SET search_path = workpulse, public;
CREATE TABLE work_sessions (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    department_id uuid NOT NULL,
    policy_version_id uuid NOT NULL,
    started_at timestamptz NOT NULL,
    ended_at timestamptz CHECK (ended_at > started_at),
    end_reason text CHECK (end_reason IN ('logout','inactivity','schedule','manual')),
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,employee_id,id),
    CHECK ((ended_at IS NULL) = (end_reason IS NULL)),
    FOREIGN KEY (organization_id,employee_id) REFERENCES employees(organization_id,id),
    FOREIGN KEY (organization_id,department_id) REFERENCES departments(organization_id,id),
    FOREIGN KEY (organization_id,policy_version_id) REFERENCES policy_versions(organization_id,id),
    EXCLUDE USING gist (organization_id WITH =,employee_id WITH =,tstzrange(started_at,ended_at,'[)') WITH &&)
);
CREATE TRIGGER published_session BEFORE INSERT OR UPDATE OF policy_version_id ON work_sessions FOR EACH ROW EXECUTE FUNCTION require_published_policy();
CREATE TABLE session_breaks (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    session_id uuid NOT NULL,
    kind text NOT NULL CHECK (kind IN ('lunch','authorized_absence','rest')),
    source text NOT NULL CHECK (source IN ('manual','schedule','agent')),
    started_at timestamptz NOT NULL,
    ended_at timestamptz CHECK (ended_at > started_at),
    PRIMARY KEY (organization_id,id),
    FOREIGN KEY (organization_id,session_id) REFERENCES work_sessions(organization_id,id),
    EXCLUDE USING gist (organization_id WITH =,session_id WITH =,tstzrange(started_at,ended_at,'[)') WITH &&)
);
CREATE UNIQUE INDEX one_lunch_per_shift ON session_breaks(organization_id,session_id) WHERE kind='lunch';
CREATE TABLE activity_events (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    device_id uuid NOT NULL,
    session_id uuid,
    client_event_id uuid NOT NULL,
    event_type text NOT NULL CHECK (event_type IN ('input','focus','idle','login','logout','heartbeat')),
    occurred_at timestamptz NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    resource_id uuid,
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,device_id,client_event_id),
    FOREIGN KEY (organization_id,employee_id,device_id) REFERENCES devices(organization_id,employee_id,id),
    FOREIGN KEY (organization_id,employee_id,session_id) REFERENCES work_sessions(organization_id,employee_id,id),
    FOREIGN KEY (organization_id,resource_id) REFERENCES resources(organization_id,id)
);
CREATE TABLE analytics_runs (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    period_start timestamptz NOT NULL,
    period_end timestamptz NOT NULL CHECK (period_end > period_start),
    algorithm_version text NOT NULL CHECK (btrim(algorithm_version) <> ''),
    status text NOT NULL CHECK (status IN ('pending','running','completed','failed')),
    created_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz,
    PRIMARY KEY (organization_id,id),
    CHECK ((status IN ('completed','failed')) = (completed_at IS NOT NULL)),
    CHECK (completed_at >= created_at)
);
CREATE TABLE activity_intervals (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    run_id uuid NOT NULL,
    session_id uuid NOT NULL,
    category text NOT NULL CHECK (category IN ('productive','non_target','neutral','idle','lunch','authorized_absence','unknown')),
    started_at timestamptz NOT NULL,
    ended_at timestamptz NOT NULL CHECK (ended_at > started_at),
    PRIMARY KEY (organization_id,id),
    FOREIGN KEY (organization_id,run_id) REFERENCES analytics_runs(organization_id,id),
    FOREIGN KEY (organization_id,session_id) REFERENCES work_sessions(organization_id,id),
    EXCLUDE USING gist (organization_id WITH =,run_id WITH =,session_id WITH =,tstzrange(started_at,ended_at,'[)') WITH &&)
);
CREATE TABLE interval_sources (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    interval_id uuid NOT NULL,
    event_id uuid NOT NULL,
    PRIMARY KEY (organization_id,interval_id,event_id),
    FOREIGN KEY (organization_id,interval_id) REFERENCES activity_intervals(organization_id,id),
    FOREIGN KEY (organization_id,event_id) REFERENCES activity_events(organization_id,id)
);
CREATE FUNCTION check_session_bounds() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE s workpulse.work_sessions%ROWTYPE;
BEGIN
    IF NEW.session_id IS NULL THEN RETURN NEW; END IF;
    SELECT * INTO s FROM workpulse.work_sessions WHERE organization_id=NEW.organization_id AND id=NEW.session_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Session does not exist in this organization' USING ERRCODE='23503'; END IF;
    IF TG_TABLE_NAME='activity_events' THEN
        IF NEW.occurred_at < s.started_at OR (s.ended_at IS NOT NULL AND NEW.occurred_at >= s.ended_at) THEN
            RAISE EXCEPTION 'Event is outside the session' USING ERRCODE='23514';
        END IF;
    ELSE
        IF NEW.started_at < s.started_at OR (s.ended_at IS NOT NULL AND (NEW.ended_at IS NULL OR NEW.ended_at > s.ended_at)) THEN
            RAISE EXCEPTION 'Interval is outside the session' USING ERRCODE='23514';
        END IF;
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER break_bounds BEFORE INSERT OR UPDATE ON session_breaks FOR EACH ROW EXECUTE FUNCTION check_session_bounds();
CREATE TRIGGER event_bounds BEFORE INSERT OR UPDATE ON activity_events FOR EACH ROW EXECUTE FUNCTION check_session_bounds();
CREATE TRIGGER interval_bounds BEFORE INSERT OR UPDATE ON activity_intervals FOR EACH ROW EXECUTE FUNCTION check_session_bounds();
CREATE FUNCTION protect_session_bounds() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM workpulse.session_breaks WHERE organization_id=OLD.organization_id AND session_id=OLD.id
        AND (started_at < NEW.started_at OR (NEW.ended_at IS NOT NULL AND (ended_at IS NULL OR ended_at > NEW.ended_at))))
    OR EXISTS (SELECT 1 FROM workpulse.activity_intervals WHERE organization_id=OLD.organization_id AND session_id=OLD.id
        AND (started_at < NEW.started_at OR (NEW.ended_at IS NOT NULL AND ended_at > NEW.ended_at)))
    OR EXISTS (SELECT 1 FROM workpulse.activity_events WHERE organization_id=OLD.organization_id AND session_id=OLD.id
        AND (occurred_at < NEW.started_at OR (NEW.ended_at IS NOT NULL AND occurred_at >= NEW.ended_at))) THEN
        RAISE EXCEPTION 'New session boundaries exclude existing records' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER session_bounds BEFORE UPDATE OF started_at,ended_at ON work_sessions FOR EACH ROW EXECUTE FUNCTION protect_session_bounds();
CREATE INDEX employee_department_idx ON employees(organization_id,department_id);
CREATE INDEX events_employee_time_idx ON activity_events(organization_id,employee_id,occurred_at DESC);
CREATE INDEX events_session_idx ON activity_events(organization_id,session_id,occurred_at);
CREATE INDEX events_received_idx ON activity_events(organization_id,received_at);
CREATE INDEX intervals_session_idx ON activity_intervals(organization_id,session_id,run_id);
