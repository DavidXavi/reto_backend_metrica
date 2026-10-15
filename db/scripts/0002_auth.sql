CREATE TABLE auth.usuario (
  id            SERIAL PRIMARY KEY,
  email         VARCHAR(150) NOT NULL UNIQUE,
  password_hash VARCHAR(200) NOT NULL,
  rol           VARCHAR(30)  NOT NULL
);

CREATE TABLE auth.refresh_token (
  id         BIGSERIAL PRIMARY KEY,
  usuario_id INT NOT NULL REFERENCES auth.usuario(id),
  token_hash CHAR(64) NOT NULL UNIQUE,
  expira     TIMESTAMPTZ NOT NULL,
  revocado   TIMESTAMPTZ NULL
);