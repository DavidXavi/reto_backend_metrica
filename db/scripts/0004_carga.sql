CREATE TABLE carga.carga_periodo (
  periodo  CHAR(7) PRIMARY KEY,
  carga_id INT NOT NULL
);

CREATE UNLOGGED TABLE carga.staging_producto (
  carga_id        INT NOT NULL,
  fila_excel      INT NOT NULL,
  periodo         CHAR(7) NOT NULL,
  codigo_producto VARCHAR(20) NOT NULL,
  nombre_producto VARCHAR(200) NOT NULL,
  precio          NUMERIC(12,2) NOT NULL
);
CREATE INDEX ix_staging_carga ON carga.staging_producto (carga_id, codigo_producto);

CREATE TABLE carga.producto (
  id              SERIAL PRIMARY KEY,
  codigo_producto VARCHAR(20) NOT NULL UNIQUE,
  nombre_producto VARCHAR(200) NOT NULL,
  precio          NUMERIC(12,2) NOT NULL,
  periodo         CHAR(7) NOT NULL,
  carga_id        INT NOT NULL,
  fila_excel      INT NOT NULL
);
CREATE INDEX ix_producto_carga ON carga.producto (carga_id);

CREATE TABLE carga.auditoria_fallo (
  id              BIGSERIAL PRIMARY KEY,
  carga_id        INT NOT NULL,
  fila_excel      INT,
  periodo         CHAR(7),
  codigo_producto VARCHAR(20),
  motivo          VARCHAR(30) NOT NULL,
  detalle         VARCHAR(500),
  fecha           TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_auditoria_carga ON carga.auditoria_fallo (carga_id);

CREATE TABLE carga.outbox (
  id             BIGSERIAL PRIMARY KEY,
  routing_key    VARCHAR(100) NOT NULL,
  payload        JSONB NOT NULL,
  correlation_id VARCHAR(100),
  creado         TIMESTAMPTZ NOT NULL DEFAULT now(),
  publicado      TIMESTAMPTZ NULL
);
CREATE INDEX ix_carga_outbox_pendiente ON carga.outbox (id) WHERE publicado IS NULL;