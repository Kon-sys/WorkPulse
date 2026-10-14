CREATE EXTENSION IF NOT EXISTS btree_gist;
SET search_path = workpulse, public;

CREATE TABLE organizations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    slug text NOT NULL UNIQUE CHECK (slug ~ '^[a-z0-9][a-z0-9-]{1,62}$'),
    name text NOT NULL CHECK (btrim(name) <> ''),
    time_zone text NOT NULL DEFAULT 'UTC',
    retention_days integer CHECK (retention_days > 0),
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE departments (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    code text NOT NULL CHECK (btrim(code) <> ''),
    name text NOT NULL CHECK (btrim(name) <> ''),
    PRIMARY KEY (organization_id, id), UNIQUE (organization_id, code)
);
CREATE TABLE employees (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    department_id uuid NOT NULL,
    employee_number text NOT NULL CHECK (btrim(employee_number) <> ''),
    full_name text NOT NULL CHECK (btrim(full_name) <> ''),
    position_name text,
    hired_on date NOT NULL,
    terminated_on date CHECK (terminated_on >= hired_on),
    PRIMARY KEY (organization_id, id), UNIQUE (organization_id, employee_number),
    FOREIGN KEY (organization_id, department_id) REFERENCES departments(organization_id, id)
);
CREATE TABLE user_accounts (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    employee_id uuid,
    email text NOT NULL CHECK (email = lower(email) AND email ~ '^[^ @]+@[^ @]+\.[^ @]+$'),
    is_active boolean NOT NULL DEFAULT true,
    external_subject text,
    PRIMARY KEY (organization_id, id), UNIQUE (organization_id, email), UNIQUE (organization_id, employee_id),
    FOREIGN KEY (organization_id, employee_id) REFERENCES employees(organization_id, id)
);
CREATE TABLE roles (
    code text PRIMARY KEY,
    name text NOT NULL
);
INSERT INTO roles VALUES ('employee','Сотрудник'), ('manager','Руководитель'), ('hr','HR'), ('admin','Администратор');
CREATE TABLE role_assignments (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    role_code text NOT NULL REFERENCES roles(code),
    scope text NOT NULL CHECK (scope IN ('organization','department','self')),
    department_id uuid,
    PRIMARY KEY (organization_id, id),
    UNIQUE NULLS NOT DISTINCT (organization_id,user_id,role_code,scope,department_id),
    CHECK ((scope = 'department') = (department_id IS NOT NULL)),
    FOREIGN KEY (organization_id,user_id) REFERENCES user_accounts(organization_id,id),
    FOREIGN KEY (organization_id,department_id) REFERENCES departments(organization_id,id)
);
CREATE TABLE devices (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    name text NOT NULL,
    platform text NOT NULL CHECK (platform IN ('windows','linux','macos')),
    registered_at timestamptz NOT NULL DEFAULT now(),
    retired_at timestamptz CHECK (retired_at >= registered_at),
    PRIMARY KEY (organization_id,id), UNIQUE (organization_id,employee_id,id),
    FOREIGN KEY (organization_id,employee_id) REFERENCES employees(organization_id,id)
);
CREATE FUNCTION validate_time_zone() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_timezone_names WHERE name = NEW.time_zone) THEN
        RAISE EXCEPTION 'Unknown IANA time zone' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER organizations_time_zone BEFORE INSERT OR UPDATE OF time_zone ON organizations
FOR EACH ROW EXECUTE FUNCTION validate_time_zone();
