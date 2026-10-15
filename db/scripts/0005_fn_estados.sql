-- Única puerta para cambiar el estado de una carga. Devuelve FALSE si la
-- transición no es válida: así un mensaje repetido no retrocede ni duplica nada.
CREATE OR REPLACE FUNCTION control.fn_cambiar_estado(
  p_id INT, p_nuevo VARCHAR, p_servicio VARCHAR, p_detalle VARCHAR DEFAULT NULL)
RETURNS BOOLEAN LANGUAGE plpgsql AS $$
DECLARE v_actual VARCHAR;
BEGIN
  SELECT estado INTO v_actual FROM control.carga_archivo WHERE id = p_id FOR UPDATE;

  IF v_actual IS NULL OR NOT (
          (v_actual = 'Pendiente'  AND p_nuevo = 'EnProceso')
       OR (v_actual = 'EnProceso'  AND p_nuevo IN ('Cargado', 'Finalizado'))
       OR (v_actual = 'Cargado'    AND p_nuevo = 'Finalizado')
       OR (v_actual = 'Finalizado' AND p_nuevo = 'Notificado')) THEN
    RETURN FALSE;
  END IF;

  UPDATE control.carga_archivo SET estado = p_nuevo WHERE id = p_id;
  INSERT INTO control.carga_estado_historial (carga_id, estado, servicio, detalle)
  VALUES (p_id, p_nuevo, p_servicio, p_detalle);
  RETURN TRUE;
END $$;

-- Cierra la carga con su resumen. Devuelve la fecha de fin, o NULL si ya estaba cerrada.
CREATE OR REPLACE FUNCTION control.fn_finalizar_carga(
  p_id INT, p_resultado VARCHAR, p_total INT, p_insertadas INT,
  p_existentes INT, p_invalidas INT, p_servicio VARCHAR, p_detalle VARCHAR DEFAULT NULL)
RETURNS TIMESTAMPTZ LANGUAGE plpgsql AS $$
DECLARE v_fin TIMESTAMPTZ := now();
BEGIN
  IF NOT control.fn_cambiar_estado(p_id, 'Finalizado', p_servicio, p_detalle) THEN
    RETURN NULL;
  END IF;

  UPDATE control.carga_archivo
     SET resultado = p_resultado, total_filas = p_total, insertadas = p_insertadas,
         existentes = p_existentes, invalidas = p_invalidas, fecha_fin = v_fin
   WHERE id = p_id;
  RETURN v_fin;
END $$;