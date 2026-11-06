# Comparación funcional por versión

La tabla compara el contenido del proyecto fuente, no versiones de un ejecutable. Las columnas describen entregas y hotfixes distribuidos o de desarrollo; los arreglos intermedios marcados como intentos no distribuidos no deben confundirse con una versión jugable entregada.

| Capacidad | v1.0 · ZIP original | v1.1 · Primera jugable | v1.2 · Herramientas | v1.3 · 30 decisiones | v1.4.0 · UX | v1.4.1–1.4.4 · Hotfixes UI | v1.4.5 · UGUI Canvas | v1.4.6 · UI compacta | v1.5.0 · UI frontal y guía  | v1.6.0 · guía + UI frontal |
|---|---|---|---|---|---|---|---|---|---|---|
| Sala 3D y modelos del centro de datos | Sí | Sí | Sí | Sí | Sí | Sí | Sí | Sí | Sin cambios; interfaz separada de la geometría  | Conservado; sin cambiar la lógica de esta capacidad. |
| Cámara de escena | Sí | Sí | Sí | Sí | Sí | Sí | Sí | Sí | Sin cambios; interfaz separada de la geometría  | Conservado; sin cambiar la lógica de esta capacidad. |
| Personaje/rig controlable | No | Sí | Sí | Sí | Sí | Sí | Sí | Sí | Conservado; ninguna modificación a locomoción  | Conservado; sin cambiar la lógica de esta capacidad. |
| Movimiento PC (WASD + ratón) | No | Sí | Sí | Sí | Sí | Sí | Sí | Sí | Conservado; ninguna modificación a locomoción  | Conservado; sin cambiar la lógica de esta capacidad. |
| Seguimiento de cabeza y locomoción XR | No | Sí | Sí | Sí | Sí | Sí | Sí | Sí | Conservado; ninguna modificación a locomoción  | Conservado; sin cambiar la lógica de esta capacidad. |
| Interacción con consolas por rayo | No | Sí | Sí | Sí | Sí | Sí | Sí | Sí | Conservado  | Conservado; sin cambiar la lógica de esta capacidad. |
| Menú, pausa y configuración | No | Sí | Sí | Sí | Sí | Sí | Sí | Sí | Conservado  | Conservado; sin cambiar la lógica de esta capacidad. |
| Incidencias principales | 0 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 incidencias en orden  | Conservado; sin cambiar la lógica de esta capacidad. |
| Pasos principales de mantenimiento | 0 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 pasos; no se modifican  | Conservado; sin cambiar la lógica de esta capacidad. |
| Herramientas seleccionables | 0 | No | 5 | 5 | 5 | 5 | 5 | 5 | Conservado  | Conservado; sin cambiar la lógica de esta capacidad. |
| Auditorías preventivas | 0 | No | Terminales iniciales | 5 × 3 fases | 5 × 3 fases | 5 × 3 fases | 5 × 3 fases | 5 × 3 fases | 5 auditorías × 3 fases; no se modifican  | Conservado; sin cambiar la lógica de esta capacidad. |
| Decisiones de mantenimiento | 0 | 15 | 15 + tareas básicas | 30 | 30 | 30 | 30 | 30 | 30 decisiones  | Conservado; sin cambiar la lógica de esta capacidad. |
| Bitácora operativa | No | Log básico | Sí | Sí | Sí | Sí | Sí | Sí | Conservado  | Conservado; sin cambiar la lógica de esta capacidad. |
| Guía del jugador paso a paso | No | No | No | Briefing básico | Se amplía el tutorial | Conservada | Parcial | Parcial | Guía completa: 5 incidencias, 5 herramientas, controles PC/VR y cierre de turno  | Conservado; sin cambiar la lógica de esta capacidad. |
| Historial de versiones | No | No | Sí | Sí | Comparativo | Ampliado | Comparativo actualizado | Comparativo actualizado | Conservado  | Conservado; sin cambiar la lógica de esta capacidad. |
| Texto de botones ligado a su acción | No aplica | Inicial | Sí | Sí | Sí | Revisado | Canvas compartido y posiciones alineadas | Mejorado | Botones UGUI de pantalla con texto hijo y raycast UI  | Conservado; sin cambiar la lógica de esta capacidad. |
| Menús, HUD y avisos responsivos | No aplica | Inicial | Parcial | Ajuste FOV/aspecto | Revisado | Migración UGUI intermedia | Canvas World Space, safe frame y fuentes verificadas | HUD arriba izq. / aviso abajo der.; safe frame | Screen Space Overlay en PC; Screen Space Camera + URP Overlay Camera Stack en XR  | Conservado; sin cambiar la lógica de esta capacidad. |
| Valores actuales de opciones visibles | No | No | No | No | Sí | Sí | Sí | Sí | Conservado  | Conservado; sin cambiar la lógica de esta capacidad. |
| Objetivo, distancia y dirección | No | Básico | Básico | Sí | Sí | Sí | Sí | HUD compacto; centro despejado | Banda fija del HUD; ya no se proyecta sobre modelos  | Conservado; sin cambiar la lógica de esta capacidad. |
| Briefing y explicación de seguridad | No | Básico | Básico | Ampliado | Reorganizado | Conservado | Conservado | Sí | Guía de jugador ampliada desde menú, briefing, pausa y F1  | Conservado; sin cambiar la lógica de esta capacidad. |
| Render de texto runtime | No | Texto inicial | Texto inicial | TMP | TMP | Migración a UGUI | UGUI Text + Liberation Sans + material predeterminado Canvas | UGUI + Liberation Sans; mismo render | UGUI Screen Space; PC Overlay / XR URP Overlay camera  | Conservado; sin cambiar la lógica de esta capacidad. |
| Diagnóstico de generación de texto | No | No | No | No | Parcial | Fuente/material | Canvas, cámara, glifos, textura y shader soportado | Sí; layout comprobado en 63 casos | Comprueba Canvas, capa UI, cámara URP apilada, depth clear y glifos  | Conservado; sin cambiar la lógica de esta capacidad. |

## ¿Se podía mover el personaje en la versión inicial?

**No.** La inspección del ZIP original muestra que no había scripts propios de control de jugador (`DCRPlayerController`, `DCRGameManager` o `DCRRuntimeBootstrap`) dentro de `Assets/Scripts`. La escena que figuraba en `EditorBuildSettings.asset` era `Assets/Scenes/DataCenter.unity`, con cámara, luz direccional, volumen global y suelo. El proyecto contenía un entorno 3D y una cámara, pero no una locomoción jugable implementada por este proyecto. El movimiento de escritorio y el soporte de seguimiento/locomoción XR se añadieron en la versión 1.1.

## Evolución por versión

### v1.0 — Proyecto base recibido

- Entorno 3D de sala de servidores, racks, climatización, iluminación, materiales y cámara de demostración.
- Sin controlador de personaje, menú de juego, misiones ni procedimientos interactivos.

### v1.1 — Primera versión jugable

- Movimiento PC, seguimiento/locomoción XR, interacción por rayo y menús.
- Cinco incidencias principales con tres pasos cada una (15 decisiones), temporizador, puntuación y resultados.

### v1.2 — Herramientas y mantenimiento preventivo

- Cinco herramientas seleccionables y terminales preventivos creados con primitivas.
- Bitácora operativa, ajustes persistentes e historial de versiones.

### v1.3 — Más contenido

- Cinco auditorías preventivas con tres fases cada una; 30 decisiones totales.
- Avance conservado al abandonar terminales, indicadores luminosos por fase y libre recorrido después de restaurar DB-03.

### v1.4.0 — Usabilidad y claridad

- Etiquetas de texto ligadas a sus botones, alineación centrada y opciones que muestran sus valores actuales.
- Briefing y prompts reorganizados para explicar movimiento, selección e interacción.
- Menú, HUD y aviso con escalado independiente según FOV y relación de aspecto.

### v1.4.1 — Hotfix intermedio TMP (no distribuido como corrección final)

- Ajustes de anclaje y tamaño de TMP, y primer diagnóstico de generación de malla.
- Intentó asignar explícitamente fuente y material SDF; posteriormente fue supersedido por la migración UGUI.

### v1.4.2 — Intento intermedio TMP/URP (no distribuido como corrección final)

- Probó un material TMP Shader Graph URP y la asignación del atlas SDF.
- Se conservó solo como historial de depuración; no forma parte de la ruta de texto actual.

### v1.4.3 — Primera migración UGUI (intento no distribuido)

- Se migró el texto runtime a `UnityEngine.UI.Text` sobre Canvas World Space.
- El material implícito y la ausencia de validación de glifos dejaron insuficiente esa iteración.

### v1.4.4 — Migración UGUI inicial (supersedida)

- Se incluyó Liberation Sans TTF en Resources y se asignó un material manual `UI/Default`.
- Añadió diagnóstico inicial de canvas, fuente, material y generación de texto.
- La interfaz seguía dependiendo de crear un material a partir del nombre de shader; v1.4.5 cambia a la ruta oficial del material predeterminado de Canvas.

### v1.4.5 — Hotfix de visibilidad UGUI

- Usa `Canvas.GetDefaultCanvasMaterial()`, material oficial que Unity entrega para dibujar gráficos UGUI, en lugar de crear un material manual con `Shader.Find`.
- Mantiene la fuente dinámica Liberation Sans incluida en `Resources` y solicita sus glifos antes de comprobar la generación.
- Después de crear el menú ejecuta diagnóstico de Canvas World Space, cámara, fuente, material renderizado, textura y caracteres visibles.
- El batch preflight de Editor construye un Canvas de prueba y genera texto español con los mismos recursos usados en runtime.
- Ajusta el sorting order de rótulos de terminal (100), panel (110), HUD (120), prompt (121) y retícula (122), evitando que etiquetas distantes se dibujen sobre el menú.
- Conserva toda la jugabilidad, misiones, auditorías y movimiento de PC/VR existentes.


### v1.4.6 — Tipografía mayor y UI compacta en esquinas (actual)

- Se aumenta la tipografía por rol en 3 px de máximo y 2 px de mínimo, manteniendo el autoajuste de las cajas.
- HUD compacto arriba a la izquierda y aviso contextual abajo a la derecha, cada uno sobre una placa oscura; la zona central queda libre para observar la sala.
- Los rótulos flotantes se hacen compactos, llevan fondo y se desplazan hacia la cámara para separarse de los racks y terminales.
- Se mantienen World Space Canvas, UGUI, escala responsiva, menús, misiones, herramientas y controles PC/VR. No se agregan assets externos.

## Siguiente trabajo propuesto (no implementado)

### v1.7.0 — Manipulación y diagnóstico físico simulado

- Paneles de estado que cambien durante cada incidencia.
- Puntos de prueba de conectividad, temperatura y alimentación.
- Cables, bandejas y componentes con interacción sencilla basada en primitivas y animaciones por código.

### v1.8.0 — Rejugabilidad

- Variantes de síntomas, modo práctica sin temporizador y modo evaluación.
- Inventario simple de repuestos y evaluación por seguridad, tiempo y evidencia.

Las funciones de v1.7.0 y v1.8.0 son propuestas futuras, no forman parte de esta entrega.


## v1.6.0 — Guía completa y UI en primer plano

- Se añadió una explicación guiada del objetivo, las cinco incidencias, las auditorías, las herramientas y el cierre del turno.
- Los paneles largos se desplazan sin ocultar los botones: rueda/Page Up/Page Down en PC y stick derecho vertical en VR.
- Menús, botones, guía, HUD y avisos comparten una interfaz de pantalla. En PC se renderiza como Overlay; en VR mediante una cámara URP Overlay con limpieza de profundidad y capa exclusiva de UI.
- Se corrige el área de la indicación de scroll para que no invada la última línea del contenido ni se superponga a los botones.
- Las funcionalidades se mantienen sin nuevos modelos, texturas ni dependencias.
