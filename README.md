# Reto técnico: carga masiva con microservicios

El usuario inicia sesión y sube un Excel de productos. El sistema lo procesa en segundo plano con RabbitMQ, guarda los datos en PostgreSQL y le avisa por correo cuando termina.

## Cómo levantarlo

```bash
cp .env.example .env
docker compose up --build
```

| Qué | Dónde |
|---|---|
| Interfaz React | http://localhost:5173 |
| API Gateway | http://localhost:8080 |
| RabbitMQ (panel) | http://localhost:15672 (usuario y clave del .env) |
| Mailpit (correos) | http://localhost:8025 |
| SeaweedFS (filer) | http://localhost:8888 |

Usuarios de prueba (clave en `CLAVE_SEMILLA` del .env):

- `cargador@reto.pe`: puede subir archivos.
- `consulta@reto.pe`: solo consulta, recibe 403 al subir.

Si el puerto 8080 está ocupado, cambia `GATEWAY_PUERTO` en el .env.

## Arquitectura

```
React / Postman
      |
API Gateway (YARP): JWT, rate limiting, CORS, correlation id
      |
      +-- Auth: login, refresh token con rotación
      +-- Control: valida, sube a SeaweedFS, guarda Pendiente, outbox
      |        |
      |        +--> RabbitMQ exchange "cargas" (direct), cola carga_masiva
      |                 |
      +-- Carga Masiva: lee el Excel, limpia, valida, COPY a staging,
      |                 fn_procesar_carga, Cargado y Finalizado, outbox
      |                 |
      |                 +--> cola notificaciones
      |                          |
      +------------------ Notificaciones: correo con MailKit, Notificado
```

- .NET 10, Clean Architecture (Domain, Application, Infrastructure, Api) en Control y Carga Masiva.
- CQRS con handlers propios (`IHandler<TRequest, TResponse>`), sin MediatR.
- PostgreSQL 17 con un esquema por servicio (`auth`, `control`, `carga`), Dapper y funciones PL/pgSQL.
- Migraciones automáticas con DbUp (contenedor `migrador`). Scripts en `db/scripts`.
- Outbox transaccional, consumidores idempotentes, ack manual, dead letter queue.
- Polly: reintentos en consumidores, circuit breaker y reintentos en el cliente de SeaweedFS.
- Serilog en JSON, manejo global de excepciones con ProblemDetails.

## Endpoints (todos por el Gateway)

| Método | Ruta | Qué hace |
|---|---|---|
| POST | /auth/login | Devuelve access token y refresh token |
| POST | /auth/refresh | Rota el refresh token |
| POST | /api/cargas | Sube el Excel (form-data, campo `archivo`). 202 con `idCarga` |
| GET | /api/cargas?pagina=1&tamano=20 | Historial del usuario |
| GET | /api/cargas/{id} | Estado, resumen y trazabilidad |
| GET | /api/cargas/{id}/archivo | Descarga el Excel original |
| GET | /api/datos/cargas/{id}/registros | Productos insertados por la carga |
| GET | /api/datos/cargas/{id}/fallos | Filas rechazadas y motivo |

Colección y entorno de Postman en `postman/`.

## Decisiones

| Tema | Decisión |
|---|---|
| Un archivo trae varios periodos (meses) | Se valida cada mes. Si uno ya está cargado o en proceso, se rechaza el archivo completo |
| Código de producto | Único en toda la tabla. Dentro del archivo gana la primera fila; las demás quedan como Existente |
| Rechazo | El estado sigue su camino (Finalizado, Notificado) y la columna `resultado` dice Rechazado |
| Celdas vacías | Nombre: "SIN NOMBRE". Precio: 0. Periodo o código vacío: fila inválida a la auditoría |
| Filas vacías | No se registran |
| Cargas simultáneas del mismo mes | La tabla `carga.carga_periodo` tiene el periodo como clave primaria. La segunda carga recibe violación de clave única y queda como PeriodoBloqueado |
| Base de datos compartida | Una instancia, un esquema por servicio. El estado se cambia solo con `control.fn_cambiar_estado` |

## Pruebas

```bash
dotnet test
```

Resultado esperado con el Excel de ejemplo: 116 insertados y 84 Existente. Si se sube otra vez, se rechaza por los 3 periodos.