CREATE TABLE control.carga_archivo (
  id             SERIAL PRIMARY KEY,
  nombre_archivo VARCHAR(200) NOT NULL,
  ruta_archivo   VARCHAR(400) NOT NULL,
  tamano_bytes   BIGINT NOT NULL,
  usuario        VARCHAR(150) NOT NULL,
  ip_origen      VARCHAR(45),
  fecha_registro TIMESTAMPTZ NOT NULL DEFAULT now(),
  estado         VARCHAR(20) NOT NULL,
  resultado      VARCHAR(20),
  total_filas    INT,
  insertadas     INT,
  existentes     INT,
  invalidas      INT,
  fecha_fin      TIMESTAMPTZ,
  CONSTRAINT ck_estado CHECK (estado IN
    ('Pendiente', 'EnProceso', 'Cargado', 'Finalizado', 'Notificado'))
);
CREATE INDEX ix_carga_archivo_usuario ON control.carga_archivo (usuario, id DESC);

CREATE TABLE control.carga_estado_historial (
  id       BIGSERIAL PRIMARY KEY,
  carga_id INT NOT NULL REFERENCES control.carga_archivo(id),
  estado   VARCHAR(20) NOT NULL,
  servicio VARCHAR(50) NOT NULL,
  detalle  VARCHAR(500),
  fecha    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE control.outbox (
  id             BIGSERIAL PRIMARY KEY,
  routing_key    VARCHAR(100) NOT NULL,
  payload        JSONB NOT NULL,
  correlation_id VARCHAR(100),
  creado         TIMESTAMPTZ NOT NULL DEFAULT now(),
  publicado      TIMESTAMPTZ NULL
);
CREATE INDEX ix_control_outbox_pendiente ON control.outbox (id) WHERE publicado IS NULL;