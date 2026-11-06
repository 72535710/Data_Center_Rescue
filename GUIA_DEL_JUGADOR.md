# Guía del jugador — Data Center Rescue 1.6.0

## Objetivo

Tu objetivo es recuperar la disponibilidad de DB-03 siguiendo procedimientos seguros. Primero debes terminar cinco incidencias principales, en orden. Después puedes completar cinco auditorías preventivas opcionales para mejorar tu puntuación. Es una simulación educativa: no se conecta ni modifica servidores reales.

## Bucle de juego

1. **Lee el HUD.** El bloque superior indica la incidencia, el paso, el objetivo, la distancia, la dirección, el tiempo, los errores y la herramienta equipada.
2. **Ve a la estación activa.** La consola de la tarea actual se identifica por su iluminación cian.
3. **Interactúa.** Acércate, apunta al panel y pulsa `E` en PC o el gatillo derecho en VR. Si no aparece el aviso, centra la mira en el panel y acércate.
4. **Lee el procedimiento.** El panel es una interfaz frontal, siempre encima de la sala. Los botones permanecen en la parte inferior y el texto puede desplazarse si no cabe en pantalla. El panel presenta tres datos: `SÍNTOMA` (qué ocurre), `QUÉ HACER` (la intervención propuesta) y `POR QUÉ` (la razón de seguridad).
5. **Selecciona una respuesta.** Elige una de tres acciones. Una respuesta correcta valida la fase y abre el siguiente paso. Una respuesta incorrecta resta puntos, explica el error y permite reintentar la misma fase.
6. **Regresa a la sala.** Al validar la fase, sigue el nuevo objetivo del HUD. No tienes que completar todas las tareas preventivas para restaurar DB-03.

## Las cinco incidencias principales

| Orden | Incidencia | Qué debes verificar |
|---:|---|---|
| 1 | Diagnóstico de base de datos | Métricas de CPU/RAM/I/O y latencia, sesiones activas y prueba de solo lectura. |
| 2 | Sustitución de SSD / RAID 1 | Confirmar la bahía B2, aislar la unidad degradada e iniciar la reconstrucción del espejo. |
| 3 | Recuperación de refrigeración | Medir temperaturas y RPM, aislar el módulo, cambiar el filtro y confirmar el flujo. |
| 4 | UPS y PDU | Revisar carga y batería, confirmar redundancia, transferir carga y validar la salida eléctrica. |
| 5 | Validación final | Comprobar replicación, prueba de integridad y documentación del ticket. |

Cada incidencia tiene tres pasos. Son quince decisiones principales.

## Auditorías preventivas

Las cinco auditorías se desbloquean después de resolver las cinco incidencias principales y restaurar DB-03. Antes de ese momento, los terminales preventivos muestran un aviso para que continúes con la estación principal. Cada auditoría requiere su herramienta y contiene tres fases. El terminal indica si llevas la herramienta correcta. Al completar las tres fases el terminal cambia a verde y recibes 120 puntos. Puedes cerrar una auditoría antes de acabar: las fases ya validadas quedan guardadas para ese turno.

| Tecla PC | Herramienta | Auditoría |
|---:|---|---|
| `1` | Escáner de red | Puertos del switch y enlace de DB-03. |
| `2` | Kit ESD | Pulsera, superficie antiestática y manipulación segura del repuesto. |
| `3` | Anemómetro | Medición del flujo de aire y tendencia térmica. |
| `4` | Multímetro | UPS, redundancia y salida eléctrica estable. |
| `5` | Tablet de servicio | Identificar activo, adjuntar evidencias y cerrar el registro. |

En VR, pulsa el botón secundario del mando izquierdo para recorrer las cinco herramientas.

## Controles

### PC

| Acción | Control |
|---|---|
| Moverse | `W`, `A`, `S`, `D` o flechas |
| Mirar | Ratón |
| Correr | `Shift` |
| Interactuar en la sala | `E` |
| Seleccionar opción de menú | Clic izquierdo |
| Navegar por menús | `↑` / `↓` o `W` / `S`; `Enter` confirma |
| Cambiar herramienta | `1` a `5` |
| Pausar / volver | `Esc` |
| Abrir esta guía | `F1` durante el juego |
| Desplazar texto largo de menús/guía | Rueda del ratón o `Page Up` / `Page Down` |

### VR / OpenXR

| Acción | Control |
|---|---|
| Mirar | Seguimiento de cabeza |
| Moverse | Stick izquierdo |
| Girar por pasos | Stick derecho |
| Interactuar / seleccionar | Gatillo derecho |
| Correr | Grip izquierdo |
| Cambiar herramienta | Botón secundario izquierdo |
| Pausar | Botón menú o principal izquierdo |
| Desplazar texto largo de menús/guía | Stick derecho arriba / abajo |

## Cerrar el turno

Cuando completes la quinta incidencia, DB-03 queda restaurado. Puedes pulsar `Esc` y elegir `CERRAR TURNO · VER RESULTADOS`, o elegir `CONTINUAR JUGANDO` para completar auditorías pendientes. Al cerrar se muestran calificación, puntuación, tiempo, errores y auditorías completadas; el mejor resultado y la bitácora se guardan localmente.

## Interfaz

Toda la interfaz se dibuja en un plano frontal de pantalla, no dentro de la geometría del centro de datos. En PC se usa `Screen Space - Overlay`, que dibuja los elementos después de la escena. En VR se usa `Screen Space - Camera` con una cámara URP Overlay apilada después de la cámara de sala; la cámara de UI renderiza únicamente la capa `UI` y limpia la profundidad de la escena antes de dibujar. Por eso racks, servidores, paneles y otros modelos no deben cubrir botones, menús, HUD o avisos. Esta estrategia requiere un Universal Renderer, que es el renderer asignado a los perfiles PC y Mobile del proyecto.

- HUD superior izquierdo: objetivo, paso, tiempo, puntuación, errores y herramienta.
- Banda de objetivo bajo el HUD: estación siguiente, distancia, orientación y herramienta requerida.
- Centro: retícula para apuntar a las consolas.
- Borde inferior: aviso de interacción o de herramienta que falta.
- Menús: cubren la pantalla y bloquean las interacciones con objetos de la sala hasta cerrarlos. Los botones quedan anclados en la franja inferior y el texto explicativo ocupa una ventana desplazable.

### Desplazamiento de paneles

Los paneles largos (guía, controles, historial y procedimientos extensos) no reducen indefinidamente la letra ni esconden los botones: se desplazan dentro de su propia ventana. En PC usa la rueda del ratón o `Page Up` / `Page Down`. En VR usa el eje vertical del stick derecho. La indicación de desplazamiento aparece en una pequeña banda situada entre el texto y los botones. El botón `VOLVER` permanece visible.

## Consejos

- No elijas una intervención solo porque parece rápida: el simulador evalúa seguridad, redundancia y evidencia.
- Lee el motivo de cada acción correcta; ayuda a entender por qué las otras opciones son riesgosas.
- Las auditorías no son necesarias para restaurar DB-03, pero sí para aspirar a la puntuación máxima.
- Si el HUD indica una herramienta requerida, equípala antes de intentar abrir esa terminal.
