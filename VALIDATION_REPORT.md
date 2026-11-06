# Informe de depuración — Data Center Rescue v1.4.6

## Cambios de esta entrega

- Todos los roles tipográficos aumentan 3 px en tamaño máximo y 2 px en mínimo de autoajuste (panel, botones, HUD, avisos, retícula y rótulos).
- El HUD queda anclado en la esquina superior izquierda y el aviso contextual en la esquina inferior derecha, con placas oscuras compactas. El centro del campo visual queda más despejado para inspeccionar servidores y consolas.
- Los rótulos de estaciones y terminales preventivos se compactan, incluyen un fondo contrastado y se desplazan 12 cm hacia la dirección horizontal de la cámara. El billboard incorpora un vector vertical alternativo cuando la cámara queda casi alineada con el eje vertical.
- Los paneles principales siguen siendo modales y centrados; su comportamiento no se altera.
- No se agregan modelos, texturas, fuentes, sonidos ni paquetes externos. Las placas son primitivas generadas en tiempo de ejecución.

## Pruebas automatizadas realizadas

Ejecutar desde la raíz del proyecto:

```powershell
python Tools/validate_project.py
```

La validación v1.4.6 informa **43/43 comprobaciones estáticas superadas**, incluidas 63 combinaciones de relación de aspecto y FOV, tamaños de tipografía, anclajes opuestos, fondos de contraste, offsets dinámicos de rótulos, contenido jugable, paquetes, escenas, recursos y GUID. Se revisaron estructuralmente 42 scripts C#; un lexer C# encontró cero tokens léxicos inválidos. La extracción limpia del ZIP debe repetir el mismo resultado.

## Compilación real y prueba visual

Este entorno de trabajo no tiene Unity Editor 6000.6.0f1 ni compilador C# instalados. Por eso no se puede afirmar que se haya completado aquí una compilación de Unity, un build de Windows/Android o una prueba visual con visor. Para realizar la compilación real en Windows, con Unity cerrado y la versión 6000.6.0f1 instalada, ejecutar:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\compile_check.ps1
```

El script invoca Unity en `-batchmode`, solicita la compilación de los assemblies y deja `Tools/CompileCheck.log`. El éxito requiere el marcador `DCR_BATCH_COMPILE_VALIDATION_PASSED` y ausencia de errores `CSxxxx`.


## v1.6.0 — validaciones adicionales

Se amplió el validador para verificar `ScrollRect`, `RectMask2D`, entrada por rueda/Page Up/Page Down y stick XR, la prueba UGUI de botón con etiqueta, y el hueco geométrico entre texto desplazable, indicación de scroll y botones (1 a 7 opciones). Para ejecutar: `python Tools/validate_project.py`. La compilación Unity Editor aún se ejecuta por separado con `Tools/compile_check.ps1`.
