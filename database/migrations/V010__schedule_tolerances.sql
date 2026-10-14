SET search_path=workpulse,public;
ALTER TABLE policy_versions ADD COLUMN late_grace_seconds integer NOT NULL DEFAULT 300 CHECK(late_grace_seconds BETWEEN 0 AND 86400);
ALTER TABLE policy_versions ADD COLUMN early_grace_seconds integer NOT NULL DEFAULT 300 CHECK(early_grace_seconds BETWEEN 0 AND 86400);
-- Existing published policies retain their historical content and receive only default tolerance metadata.
