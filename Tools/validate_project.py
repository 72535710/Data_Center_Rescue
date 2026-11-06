#!/usr/bin/env python3
"""Static validation for Data Center Rescue 1.5.0.

This script does not pretend to compile Unity assemblies. It validates project
metadata, C# lexical structure, the front-screen UGUI architecture, guide flow,
mission content, URP camera-stack configuration in source and project assets.
Run from any directory: python Tools/validate_project.py
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCRIPT_DIR = ROOT / "Assets" / "Scripts"
MANAGER = SCRIPT_DIR / "DCRGameManager.cs"
UI_STYLE = SCRIPT_DIR / "DCRUIStyle.cs"
PLAYER = SCRIPT_DIR / "DCRPlayerController.cs"
INTERACTABLE = SCRIPT_DIR / "DCRWorldInteractable.cs"
EDITOR_SETUP = ROOT / "Assets" / "Editor" / "DCRProjectSetup.cs"

passed: list[str] = []
failed: list[str] = []

def check(name: str, condition: bool, detail: str = "") -> None:
    (passed if condition else failed).append(name + (f" — {detail}" if detail else ""))

def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace") if path.exists() else ""


def scan_csharp(path: Path) -> tuple[bool, str]:
    """Check balanced delimiters and terminated comments/strings/chars."""
    source = read(path)
    if not source:
        return False, "archivo vacío o ausente"
    stack: list[tuple[str, int]] = []
    pairs = {")": "(", "]": "[", "}": "{"}
    state = "code"
    i = 0
    line = 1
    while i < len(source):
        ch = source[i]
        nxt = source[i + 1] if i + 1 < len(source) else ""
        if ch == "\n":
            line += 1
        if state == "code":
            if ch == "/" and nxt == "/":
                state = "line_comment"
                i += 2
                continue
            if ch == "/" and nxt == "*":
                state = "block_comment"
                i += 2
                continue
            # Verbatim C# strings are not currently used in this project, but
            # support them so future text additions do not create false alarms.
            if ch == "@" and nxt == '"':
                state = "verbatim_string"
                i += 2
                continue
            if ch == '"':
                state = "string"
                i += 1
                continue
            if ch == "'":
                state = "char"
                i += 1
                continue
            if ch in "([{":
                stack.append((ch, line))
            elif ch in ")]}":
                if not stack or stack[-1][0] != pairs[ch]:
                    return False, f"delimitador {ch} sin pareja en línea {line}"
                stack.pop()
        elif state == "line_comment":
            if ch == "\n":
                state = "code"
        elif state == "block_comment":
            if ch == "*" and nxt == "/":
                state = "code"
                i += 2
                continue
        elif state == "string":
            if ch == "\\":
                i += 2
                continue
            if ch == '"':
                state = "code"
            elif ch == "\n":
                return False, f"cadena ordinaria sin cerrar en línea {line}"
        elif state == "verbatim_string":
            if ch == '"' and nxt == '"':
                i += 2
                continue
            if ch == '"':
                state = "code"
        elif state == "char":
            if ch == "\\":
                i += 2
                continue
            if ch == "'":
                state = "code"
            elif ch == "\n":
                return False, f"literal char sin cerrar en línea {line}"
        i += 1
    if state in ("string", "verbatim_string", "char", "block_comment"):
        return False, f"estado léxico sin cerrar: {state}"
    if stack:
        token, start_line = stack[-1]
        return False, f"delimitador {token} abierto en línea {start_line}"
    # Preprocessor directives must be paired, even when excluded at runtime.
    depth = 0
    for number, raw in enumerate(source.splitlines(), 1):
        directive = re.match(r"^\s*#\s*(if|endif)\b", raw)
        if directive:
            if directive.group(1) == "if":
                depth += 1
            else:
                depth -= 1
                if depth < 0:
                    return False, f"#endif inesperado en línea {number}"
    if depth != 0:
        return False, f"directivas #if/#endif desbalanceadas ({depth})"
    return True, ""

# 01-05: project metadata and package pins
manifest_path = ROOT / "Packages" / "manifest.json"
try:
    manifest = json.loads(read(manifest_path))
    deps = manifest.get("dependencies", {})
    manifest_ok = all(deps.get(k) == v for k, v in {
        "com.unity.inputsystem": "1.20.0",
        "com.unity.render-pipelines.universal": "17.6.0",
        "com.unity.ugui": "2.6.0",
        "com.unity.xr.management": "4.7.0",
        "com.unity.xr.openxr": "1.19.0",
    }.items())
except Exception as exc:
    deps = {}
    manifest_ok = False
check("01 · manifiesto JSON y dependencias fijadas", manifest_ok, str(deps))
check("02 · Unity 6000.6.0f1", "m_EditorVersion: 6000.6.0f1" in read(ROOT / "ProjectSettings" / "ProjectVersion.txt"))
check("03 · VERSION 1.6.0", read(ROOT / "VERSION").strip() == "1.6.0")
check("04 · Player Settings 1.6.0", "bundleVersion: 1.6.0" in read(ROOT / "ProjectSettings" / "ProjectSettings.asset"))
build_settings = read(ROOT / "ProjectSettings" / "EditorBuildSettings.asset")
check("05 · escena jugable habilitada", "enabled: 1" in build_settings and "path: Assets/Scenes/DataCenter_Demo.unity" in build_settings)

# 06-10: source files and C# lexical scan
cs_files = sorted(list(SCRIPT_DIR.glob("*.cs")) + list((ROOT / "Assets" / "Editor").glob("*.cs")))
check("06 · scripts runtime/editor encontrados", len(cs_files) >= 6, f"{len(cs_files)} archivos")
lex_errors = []
for file in cs_files:
    ok, why = scan_csharp(file)
    if not ok:
        lex_errors.append(f"{file.relative_to(ROOT)}: {why}")
check("07 · delimitadores, comentarios y cadenas C#", not lex_errors, "; ".join(lex_errors[:5]))
manager = read(MANAGER)
style = read(UI_STYLE)
player = read(PLAYER)
interactable = read(INTERACTABLE)
editor = read(EDITOR_SETUP)
check("08 · aliases XR explícitos en controlador y manager",
      "XRCommonUsages = UnityEngine.XR.CommonUsages" in manager and
      "XRCommonUsages = UnityEngine.XR.CommonUsages" in player and
      "using XRInputDevice = UnityEngine.XR.InputDevice" in manager and
      "using XRInputDevice = UnityEngine.XR.InputDevice" in player)
check("09 · UI runtime usa UnityEngine.UI, no TMP",
      "using UnityEngine.UI;" in manager and "using TMPro;" not in manager and "TMP_Text" not in manager)
check("10 · Resource de fuente runtime presente",
      (ROOT / "Assets" / "DCRRuntimeUI" / "Resources" / "DCRLiberationSans.ttf").exists() and
      (ROOT / "Assets" / "DCRRuntimeUI" / "Resources" / "DCRLiberationSans.ttf.meta").exists())

# 11-19: screen-space UI architecture
check("11 · Canvas de PC Screen Space Overlay", "RenderMode.ScreenSpaceOverlay" in style and "canvas.renderMode = RenderMode.ScreenSpaceOverlay" in style)
check("12 · Canvas XR Screen Space Camera", "RenderMode.ScreenSpaceCamera" in style and "canvas.renderMode = RenderMode.ScreenSpaceCamera" in style)
check("13 · CanvasScaler adaptable", "ScaleWithScreenSize" in style and "ReferenceWidth = 1920" in style and "ReferenceHeight = 1080" in style)
check("14 · Canvas sorting alto y capa UI", "CanvasSortOrder = 32760" in style and "UILayer = 5" in style and "canvas.sortingOrder = CanvasSortOrder" in style)
check("15 · distancia frontal del Canvas XR", "XRCanvasPlaneDistance = 1.2f" in style)
check("16 · creación de UI centralizada", "CreateScreenCanvas" in style and "CreateImage" in style and "CreateText" in style)
check("17 · GameManager no crea Canvas World Space", "RenderMode.WorldSpace" not in manager)
check("18 · menús y botones en capa UI", "panelRoot.layer = DCRUIStyle.UILayer" in manager and "buttonRect" in manager and "CreateUIText(buttonRect" in manager)
check("19 · raycast UI no bloqueado por geometría", "GraphicRaycaster.BlockingObjects.None" in style and "overlayRaycaster.Raycast" in manager)

# 20-27: XR overlay-camera stack and runtime diagnostics
check("20 · URP camera-stack API usada", "using UnityEngine.Rendering.Universal;" in manager and "baseCameraData.cameraStack" in manager)
check("21 · cámara de UI declarada Overlay", "overlayData.renderType = CameraRenderType.Overlay" in manager)
check("22 · cámara de UI limita culling a UI", "xrOverlayCamera.cullingMask = 1 << DCRUIStyle.UILayer" in manager)
check("23 · limpieza de profundidad en cámara XR", "overlayData.clearDepth" in manager and "CameraClearFlags.Nothing" in manager)
check("24 · cámara Overlay apilada después de Base", "stack.Add(xrOverlayCamera)" in manager and "baseCameraData.renderType = CameraRenderType.Base" in manager)
check("25 · canvas XR asignado a la cámara Overlay", "DCRUIStyle.CreateScreenCanvas(xrActive ? uiRenderCamera : null, xrActive)" in manager and "canvas.worldCamera = camera" in style)
check("26 · diagnóstico de cámara, capa y textos", all(x in manager for x in ["DCR_UI_DIAGNOSTIC", "failedTexts", "wrongLayerGraphics", "xrStackValid", "DCR_UI_FATAL"]))
check("27 · batch preflight comprueba cámara Overlay URP", all(x in editor for x in ["CameraRenderType.Overlay", "cameraStack.Add(overlayCamera)", "overlayCameraData.clearDepth", "xrOverlayCameraStack"]))

# 28-35: layout, guide, controls and gameplay clarity
check("28 · panel usa anchors normalizados", "new Vector2(0.025f, 0.025f)" in manager and "new Vector2(0.975f, 0.975f)" in manager)
check("29 · botones se dimensionan por fracción de pantalla", "float buttonHeight = optionCount >= 7 ? 0.052f" in manager and "float yMin = buttonBottom + i * (buttonHeight + buttonGap)" in manager)
check("30 · objetivo en banda fija del HUD", "DCR_ObjectiveBanner" in manager and "new Vector2(0f, 1f)" in manager and "WorldToScreenPoint(worldMarker)" not in manager)
check("31 · prompt inferior y retícula central", "DCR_InteractionPrompt" in manager and "new Vector2(0.5f, 0f)" in manager and "DCR_Crosshair" in manager)
check("32 · guía en menú, briefing, pausa y F1", all(x in manager for x in ["GUÍA DEL JUGADOR", "LEER GUÍA COMPLETA", "ShowPlayerGuide(ScreenMode.Pause)", "keyboard.f1Key.wasPressedThisFrame"]))
check("33 · guía describe misiones y herramientas", all(x in manager for x in ["Diagnóstico de base de datos", "SSD / RAID 1", "Recuperación de refrigeración", "Escáner de red", "Kit ESD", "Anemómetro", "Multímetro", "Tablet de servicio"]))
check("34 · guía de controles PC/VR y cierre", all(x in manager for x in ["WASD/flechas mover", "stick izquierdo mover", "gatillo derecho", "CERRAR TURNO"]))
check("35 · botón guía desde briefing devuelve al briefing", "ShowPlayerGuide(ScreenMode.Tutorial)" in manager and "guideReturnMode == ScreenMode.Tutorial" in manager)

# 36-42: gameplay content and regression checks
check("36 · cinco incidencias principales", manager.count("new MissionDefinition(") == 5, f"{manager.count('new MissionDefinition(')} definiciones")
check("37 · cinco auditorías preventivas", manager.count("new SideTaskDefinition(") == 5, f"{manager.count('new SideTaskDefinition(')} definiciones")
check("38 · progreso opcional conservado y terminales visuales", "sideTaskProgress" in manager and "SetProgress(" in manager and "SetCompleted(true)" in manager)
check("39 · PC y VR conservan entrada e interacción", all(x in manager for x in ["Mouse.current", "Keyboard.current", "XRCommonUsages.trigger", "GetXRControllerRay"]))
check("40 · no hay clase/botón 3D para menús", "CreatePrimitive(PrimitiveType.Cube)" not in manager[manager.find("private void BuildPanel"):manager.find("private void StartMission")])
check("41 · guía externa y documentación actualizadas", all((ROOT / name).exists() for name in ["GUIA_DEL_JUGADOR.md", "README.md", "CHANGELOG.md", "VERSION_HISTORY.md"])) and "1.5.0" in read(ROOT / "README.md")
check("42 · sin Timeline ni XRI sin usar", "com.unity.timeline" not in json.dumps(deps) and "com.unity.xr.interaction.toolkit" not in json.dumps(deps))

# 43-48: package settings, scene and version metadata
for index, pipeline in enumerate(["PC_RPAsset.asset", "Mobile_RPAsset.asset"], 43):
    content = read(ROOT / "Assets" / "Settings" / pipeline)
    check(f"{index:02d} · {pipeline} usa Universal Renderer", "m_RendererDataList:" in content and "m_DefaultRendererIndex: 0" in content)
quality = read(ROOT / "ProjectSettings" / "QualitySettings.asset")
check("45 · URP asignado a perfiles PC y Mobile", "PC_RPAsset" in read(ROOT / "Assets" / "Settings" / "PC_RPAsset.asset") and
      "customRenderPipeline:" in quality and "PC" in quality and "Mobile" in quality)
# GUID duplicate scan (all asset metas)
guid_map: dict[str, Path] = {}
duplicates: list[str] = []
for meta in ROOT.rglob("*.meta"):
    if any(part in {"Library", ".git", "Temp", "Obj"} for part in meta.parts):
        continue
    match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", read(meta), re.M)
    if match:
        guid = match.group(1)
        if guid in guid_map:
            duplicates.append(f"{guid}: {guid_map[guid].relative_to(ROOT)} / {meta.relative_to(ROOT)}")
        else:
            guid_map[guid] = meta
check("46 · GUID sin duplicados", not duplicates, f"{len(guid_map)} GUID; " + "; ".join(duplicates[:2]))
# Scene path exists and scene meta agrees with BuildSettings guid
scene_path = ROOT / "Assets" / "Scenes" / "DataCenter_Demo.unity"
scene_meta = read(Path(str(scene_path) + ".meta"))
scene_guid = re.search(r"^guid:\s*([0-9a-f]{32})", scene_meta, re.M)
build_guid = re.search(r"path: Assets/Scenes/DataCenter_Demo.unity\s+guid: ([0-9a-f]{32})", build_settings)
check("47 · escena y GUID de Build Settings coinciden", scene_path.exists() and scene_guid is not None and build_guid is not None and scene_guid.group(1) == build_guid.group(1))
# Static layout math: normalized panel and button group must leave a body area.
for count in range(1, 8):
    h = 0.052 if count >= 7 else 0.060 if count >= 6 else 0.070 if count >= 5 else 0.083 if count >= 4 else 0.095 if count >= 3 else 0.105 if count == 2 else 0.12
    gap = 0.012 if count >= 6 else 0.017
    bottom = 0.045 + count * h + (count - 1) * gap + 0.025
    if bottom > 0.675:
        gap = max(0.006, gap - 0.004)
        bottom = 0.045 + count * h + (count - 1) * gap + 0.02
    if 0.835 - bottom < 0.15:
        failed.append(f"48 · área de lectura suficiente con {count} botones — body height {0.835-bottom:.3f}")
        break
else:
    passed.append("48 · área de lectura suficiente con 1–7 botones")

# 49-53: clarity, scroll and always-on-top UI contract
check("49 · ScrollRect usa viewport y máscara de recorte", all(x in manager for x in ["AddComponent<ScrollRect>()", "AddComponent<RectMask2D>()", "bodyScrollRect.viewport = bodyViewport", "bodyScrollRect.content = bodyContent"]))
check("50 · desplazamiento PC por rueda y Page Up/Down", all(x in manager for x in ["mouse.scroll.ReadValue().y", "keyboard.pageDownKey.wasPressedThisFrame", "keyboard.pageUpKey.wasPressedThisFrame"]))
check("51 · desplazamiento XR por stick derecho", "XRCommonUsages.primary2DAxis" in manager and "bodyScrollRect.verticalNormalizedPosition" in manager)
check("52 · aviso de desplazamiento separado de botones", "Menu_Scroll_Hint_Plate" in manager and "bodyBottom - 0.024f" in manager and "bodyBottom - 0.002f" in manager)
check("53 · guía externa documenta scroll PC y VR", all(x in read(ROOT / "GUIA_DEL_JUGADOR.md") for x in ["Page Up", "Page Down", "Stick derecho arriba / abajo", "plano frontal de pantalla"]))
check("54 · prueba batch de Editor construye ScrollRect y botón UGUI", all(x in editor for x in ["DCR_UI_BatchScrollViewport", "AddComponent<RectMask2D>()", "AddComponent<ScrollRect>()", "DCR_UI_BatchButtonLabel", "menuFlowReady"]))

# 55: the scroll hint must sit strictly in the gap between the viewport and buttons.
layout_ok = True
layout_details = []
for count in range(1, 8):
    h = 0.052 if count >= 7 else 0.060 if count >= 6 else 0.070 if count >= 5 else 0.083 if count >= 4 else 0.095 if count >= 3 else 0.105 if count == 2 else 0.12
    gap = 0.012 if count >= 6 else 0.017
    button_bottom = 0.045
    button_top = button_bottom + (count - 1) * (h + gap) + h
    body_bottom = button_bottom + count * h + (count - 1) * gap + 0.025
    if body_bottom > 0.835 - 0.16:
        gap = max(0.006, gap - 0.004)
        button_top = button_bottom + (count - 1) * (h + gap) + h
        body_bottom = button_bottom + count * h + (count - 1) * gap + 0.02
    hint_bottom = body_bottom - 0.024
    hint_top = body_bottom - 0.002
    if hint_bottom < button_top + 0.0005 or hint_top > body_bottom - 0.001:
        layout_ok = False
        layout_details.append(f"{count} botones: buttonTop={button_top:.3f}, hint={hint_bottom:.3f}..{hint_top:.3f}, bodyBottom={body_bottom:.3f}")
check("55 · franja de scroll no se cruza con botones ni texto (1–7 opciones)", layout_ok, "; ".join(layout_details))
check("56 · altura del contenido se mide con Text.preferredHeight", "bodyText.preferredHeight" in manager and "estimatedCharactersPerLine" not in manager)
check("57 · auditorías bloqueadas hasta recuperar DB-03", "if (!mainServiceComplete)" in manager and "AUDITORÍAS PREVENTIVAS BLOQUEADAS" in manager and "DISPONIBLE DESPUÉS DE RESTAURAR DB-03" in manager)
history_text = read(ROOT / "VERSION_HISTORY.md")
version_table = [line for line in history_text.splitlines() if line.startswith("|") and line.endswith("|")]
table_cells = [len(line.split("|")[1:-1]) for line in version_table]
check("58 · tabla comparativa de versiones alineada a v1.6.0", len(table_cells) >= 20 and len(set(table_cells)) == 1 and table_cells[0] == 11 and "v1.6.0 · guía + UI frontal" in version_table[0])

print(f"Data Center Rescue v1.6.0 — validación estática")
print(f"Raíz: {ROOT}")
print(f"Pruebas superadas: {len(passed)}")
for item in passed:
    print(f"[OK] {item}")
if failed:
    print(f"Pruebas fallidas: {len(failed)}")
    for item in failed:
        print(f"[ERROR] {item}")
    sys.exit(1)
print("RESULTADO: TODAS LAS PRUEBAS ESTÁTICAS SUPERADAS")
print("NOTA: esta validación no sustituye una compilación de Unity Editor ni una prueba física XR.")
