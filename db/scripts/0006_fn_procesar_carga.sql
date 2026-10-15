-- Aplica las reglas de negocio sobre las filas que ya están en staging.
-- Corre dentro de la transacción del worker: todo se confirma junto o nada.
CREATE OR REPLACE FUNCTION carga.fn_procesar_carga(p_carga_id INT)
RETURNS TABLE (resultado VARCHAR, insertadas INT, existentes INT)
LANGUAGE plpgsql AS $$
DECLARE v_total INT; v_insertadas INT;
BEGIN
  -- 1. Regla de periodo: ¿algún mes del archivo ya tiene dueño?
  INSERT INTO carga.auditoria_fallo (carga_id, periodo, motivo, detalle)
  SELECT p_carga_id, s.periodo,
         CASE WHEN ca.estado IN ('Pendiente', 'EnProceso')
              THEN 'PeriodoBloqueado' ELSE 'PeriodoRechazado' END,
         'Periodo tomado por la carga ' || ca.id
  FROM (SELECT DISTINCT st.periodo FROM carga.staging_producto st
        WHERE st.carga_id = p_carga_id) s
  JOIN carga.carga_periodo cp ON cp.periodo = s.periodo
  JOIN control.carga_archivo ca ON ca.id = cp.carga_id;

  IF FOUND THEN
    DELETE FROM carga.staging_producto st WHERE st.carga_id = p_carga_id;
    RETURN QUERY SELECT 'Rechazado'::VARCHAR, 0, 0;
    RETURN;
  END IF;

  -- 2. Tomar los meses. Si otra carga los tomó en paralelo, la PRIMARY KEY
  --    lanza unique_violation (23505) y el worker lo traduce a PeriodoBloqueado.
  INSERT INTO carga.carga_periodo (periodo, carga_id)
  SELECT DISTINCT st.periodo, p_carga_id FROM carga.staging_producto st
  WHERE st.carga_id = p_carga_id;

  -- 3. Regla de código: ya existe en la tabla o se repite antes en el mismo archivo
  INSERT INTO carga.auditoria_fallo
    (carga_id, fila_excel, periodo, codigo_producto, motivo, detalle)
  SELECT s.carga_id, s.fila_excel, s.periodo, s.codigo_producto, 'Existente',
         'El código ya está registrado'
  FROM carga.staging_producto s
  WHERE s.carga_id = p_carga_id
    AND (EXISTS (SELECT 1 FROM carga.producto p
                 WHERE p.codigo_producto = s.codigo_producto)
      OR EXISTS (SELECT 1 FROM carga.staging_producto d
                 WHERE d.carga_id = s.carga_id
                   AND d.codigo_producto = s.codigo_producto
                   AND d.fila_excel < s.fila_excel));

  -- 4. Insertar la primera aparición de cada código nuevo
  INSERT INTO carga.producto
    (codigo_producto, nombre_producto, precio, periodo, carga_id, fila_excel)
  SELECT DISTINCT ON (st.codigo_producto)
         st.codigo_producto, st.nombre_producto, st.precio, st.periodo, st.carga_id, st.fila_excel
  FROM carga.staging_producto st
  WHERE st.carga_id = p_carga_id
  ORDER BY st.codigo_producto, st.fila_excel
  ON CONFLICT (codigo_producto) DO NOTHING;
  GET DIAGNOSTICS v_insertadas = ROW_COUNT;

  SELECT count(*) INTO v_total FROM carga.staging_producto st WHERE st.carga_id = p_carga_id;
  DELETE FROM carga.staging_producto st WHERE st.carga_id = p_carga_id;

  RETURN QUERY SELECT
    (CASE WHEN v_insertadas = v_total THEN 'Exitoso' ELSE 'ConObservaciones' END)::VARCHAR,
    v_insertadas, v_total - v_insertadas;
END $$;