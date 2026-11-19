-- FilaCero: esquema simplificado para integración académica (sin datos reales).
CREATE TABLE IF NOT EXISTS patients (
  id uuid PRIMARY KEY,
  full_name text NOT NULL,
  clinic text NOT NULL,
  curp_hash text,
  nss_hash text,
  phone_last4 text,
  document_name text,
  document_mime text,
  document_bytes bytea,
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS triage_evaluations (
  patient_id uuid PRIMARY KEY REFERENCES patients(id) ON DELETE CASCADE,
  level text NOT NULL,
  color text NOT NULL,
  title text NOT NULL,
  message text NOT NULL,
  answers_json jsonb NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE SEQUENCE IF NOT EXISTS turn_number_seq START 1;
CREATE TABLE IF NOT EXISTS turns (
  id uuid PRIMARY KEY,
  patient_id uuid NOT NULL UNIQUE REFERENCES patients(id),
  code text NOT NULL UNIQUE,
  clinic text NOT NULL,
  priority text NOT NULL,
  priority_color text NOT NULL,
  status text NOT NULL DEFAULT 'En espera',
  room text,
  nonce text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_turns_queue ON turns(clinic, status, created_at);
