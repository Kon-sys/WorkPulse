SET search_path = workpulse, public;
CREATE TABLE monitoring_policies (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    name text NOT NULL CHECK (btrim(name) <> ''),
    archived boolean NOT NULL DEFAULT false,
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,name)
);
CREATE TABLE policy_versions (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    policy_id uuid NOT NULL,
    version_number integer NOT NULL CHECK (version_number > 0),
    idle_limit_seconds integer NOT NULL CHECK (idle_limit_seconds > 0),
    non_target_limit_seconds integer NOT NULL CHECK (non_target_limit_seconds > 0),
    lunch_limit_seconds integer NOT NULL CHECK (lunch_limit_seconds > 0),
    time_zone text NOT NULL,
    published_at timestamptz,
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,policy_id,version_number),
    FOREIGN KEY (organization_id,policy_id) REFERENCES monitoring_policies(organization_id,id)
);
CREATE TRIGGER policy_time_zone BEFORE INSERT OR UPDATE OF time_zone ON policy_versions
FOR EACH ROW EXECUTE FUNCTION validate_time_zone();
CREATE TABLE schedule_days (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    policy_version_id uuid NOT NULL,
    iso_weekday smallint NOT NULL CHECK (iso_weekday BETWEEN 1 AND 7),
    starts_at time NOT NULL,
    ends_at time NOT NULL,
    ends_next_day boolean NOT NULL DEFAULT false,
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,policy_version_id,iso_weekday),
    CHECK ((NOT ends_next_day AND ends_at > starts_at) OR (ends_next_day AND ends_at <= starts_at)),
    FOREIGN KEY (organization_id,policy_version_id) REFERENCES policy_versions(organization_id,id)
);
CREATE TABLE resources (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    kind text NOT NULL CHECK (kind IN ('application','domain')),
    identifier text NOT NULL CHECK (identifier = lower(identifier) AND btrim(identifier) <> ''),
    display_name text NOT NULL,
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,kind,identifier),
    CHECK (kind <> 'domain' OR identifier ~ '^[a-z0-9.-]+$')
);
CREATE TABLE classification_rules (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    policy_version_id uuid NOT NULL,
    resource_id uuid NOT NULL,
    category text NOT NULL CHECK (category IN ('productive','non_target','neutral')),
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,policy_version_id,resource_id),
    FOREIGN KEY (organization_id,policy_version_id) REFERENCES policy_versions(organization_id,id),
    FOREIGN KEY (organization_id,resource_id) REFERENCES resources(organization_id,id)
);
CREATE TABLE policy_assignments (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    policy_version_id uuid NOT NULL,
    scope text NOT NULL CHECK (scope IN ('organization','department','employee')),
    department_id uuid,
    employee_id uuid,
    valid_from timestamptz NOT NULL,
    valid_until timestamptz CHECK (valid_until > valid_from),
    PRIMARY KEY (organization_id,id),
    CHECK ((scope='organization' AND department_id IS NULL AND employee_id IS NULL)
        OR (scope='department' AND department_id IS NOT NULL AND employee_id IS NULL)
        OR (scope='employee' AND employee_id IS NOT NULL AND department_id IS NULL)),
    FOREIGN KEY (organization_id,policy_version_id) REFERENCES policy_versions(organization_id,id),
    FOREIGN KEY (organization_id,department_id) REFERENCES departments(organization_id,id),
    FOREIGN KEY (organization_id,employee_id) REFERENCES employees(organization_id,id),
    EXCLUDE USING gist (organization_id WITH =,scope WITH =,
        (coalesce(department_id,employee_id,organization_id)) WITH =,
        tstzrange(valid_from,valid_until,'[)') WITH &&)
);
CREATE FUNCTION protect_policy_version() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE version_id uuid; tenant_id uuid; is_published boolean;
BEGIN
    IF TG_TABLE_NAME = 'policy_versions' THEN
        IF OLD.published_at IS NOT NULL THEN
            RAISE EXCEPTION 'Published versions are immutable; create a new version' USING ERRCODE='23514';
        END IF;
    ELSE
        IF TG_OP <> 'INSERT' THEN
            SELECT published_at IS NOT NULL INTO is_published FROM workpulse.policy_versions
            WHERE organization_id=OLD.organization_id AND id=OLD.policy_version_id FOR UPDATE;
            IF is_published THEN RAISE EXCEPTION 'Published policy content is immutable' USING ERRCODE='23514'; END IF;
        END IF;
        IF TG_OP <> 'DELETE' THEN
            SELECT published_at IS NOT NULL INTO is_published FROM workpulse.policy_versions
            WHERE organization_id=NEW.organization_id AND id=NEW.policy_version_id FOR UPDATE;
            IF is_published THEN RAISE EXCEPTION 'Published policy content is immutable' USING ERRCODE='23514'; END IF;
        END IF;
    END IF;
    IF TG_OP='DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END $$;
CREATE TRIGGER immutable_version BEFORE UPDATE OR DELETE ON policy_versions FOR EACH ROW EXECUTE FUNCTION protect_policy_version();
CREATE TRIGGER immutable_schedule BEFORE INSERT OR UPDATE OR DELETE ON schedule_days FOR EACH ROW EXECUTE FUNCTION protect_policy_version();
CREATE TRIGGER immutable_rules BEFORE INSERT OR UPDATE OR DELETE ON classification_rules FOR EACH ROW EXECUTE FUNCTION protect_policy_version();
CREATE FUNCTION require_published_policy() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM 1 FROM workpulse.policy_versions WHERE organization_id=NEW.organization_id
        AND id=NEW.policy_version_id AND published_at IS NOT NULL FOR SHARE;
    IF NOT FOUND THEN RAISE EXCEPTION 'An effective assignment/session requires a published policy' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER published_assignment BEFORE INSERT OR UPDATE ON policy_assignments FOR EACH ROW EXECUTE FUNCTION require_published_policy();
