# Guía de ejecución de pruebas - Laboratorio 6 (NGO 2.13.2 / Unity 6000.3.10f1)

> **Aviso.** Las pruebas multijugador **no** se han ejecutado en este entorno (solo se verificó
> compilación y análisis estático). Los criterios de comportamiento siguen **pendientes** hasta que
> completes esta guía. No dar el laboratorio por verificado antes de hacerlo.

## 1. Antes de entrar en Play

1. Abrir `C:\Users\USER\Multiplayer_Lab5` con Unity **6000.3.10f1**.
2. Abrir `Assets/Scenes/SampleScene.unity`. Si Unity pide recargar la escena, elegir **Reload**.
3. Seleccionar el GameObject **`NetworkManager`** y comprobar en el Inspector:
   - **Player Prefab** = `Assets/Prefab/Player.prefab`.
   - **Connection Approval** = activado.
   - Componente **`Lab6RoleManager`** con **Chest Prefab** = `Assets/Prefab/Chest.prefab`.
4. Si acabas de reimportar cambios, salir y volver a entrar en Play para que Unity recargue los
   prefabs y scripts.

## 2. Arranque (menú en pantalla, ya no hace falta el Inspector)

Al entrar en Play mode aparece un panel arriba a la izquierda en la **Game view**:

```
EL COFRE DEL GUARDIÁN
Sin conexión. Inicia una instancia:
[ Start Host  (H) ]
[ Start Client  (C) ]
[ Start Server ]
2 jugadores: Multiplayer Play Mode (Host en un editor, Client en el otro).
```

- **Start Host (H):** servidor + jugador local (Host).
- **Start Client (C):** cliente que se conecta a `127.0.0.1:7777`.
- **Start Server:** servidor dedicado (sin jugador local).
- **Shutdown (X):** detiene la instancia (botón del HUD o tecla X).

### A) Dos jugadores (Host + 1 Client)
1. Menú **Window > Play Mode > Scenarios** > **Configure play mode scenarios**.
2. Crear un escenario y añadir **1 Additional Editor Instance**.
3. Entrar en **Play Mode**: se abren el editor principal y el adicional.
4. En la Game view del **editor principal** pulsar **Start Host**.
5. En la Game view del **editor adicional** (foco con **Ctrl+F10**) pulsar **Start Client**.

> Alternativa sin MPPM: en el editor pulsa **Start Host**; compila el proyecto y ejecuta el `.exe`,
> y en el `.exe` pulsa **Start Client**. (La escena ya está en Build Settings.)

### B) Promoción de Guardián (3 instancias)
1. Escenario MPPM con Editor principal + **2 Additional Editor Instances**.
2. Principal: **Start Server**. Adicional 1: **Start Client** (será Guardián). Adicional 2:
   **Start Client** (Explorador).
3. En el Adicional 1 pulsa **Shutdown (X)**. El Adicional 2 debe pasar a **Guardián** (HUD dorado +
   consola).

## 3. Teclas

| Tecla | Acción |
|-------|--------|
| **W A S D** | Mover |
| **Espacio** | Interactuar: abrir/cancelar cofre, recoger cubo, reclamar botín libre |
| **G** | Pedir cofre (**solo Guardián**) |
| **E** o **clic derecho** | Lanzar el cubo sostenido |
| **T** | Transferir el cubo sostenido al jugador más cercano |
| **Q** | Consumir el cubo sostenido |
| **H / C / X** | Start Host / Start Client / Shutdown |

### Qué muestra el HUD durante la partida
- **Título**: `EL COFRE DEL GUARDIÁN`.
- **Conexión**: `HOST`, `SERVIDOR` o `CLIENTE`, estado (`ACTIVO`/`CONECTADO`/`CONECTANDO...`) y `ClientId`.
- **Rol**: `GUARDIÁN` en **dorado** o `EXPLORADOR` en **cian**.
- **Guardián actual**: `J0`, `J1`, ... (visible también desde los clientes).
- **Contadores**: `Cubos propios` y `Cubos libres en el suelo`.
- **Controles**: lista de teclas.
- **Mensajes temporales** (abajo, se desvanecen): cofre invocado/abierto, botín obtenido, cubo
  transferido, cubo consumido, botín reclamado, rechazos y desconexión/promoción.
- **Etiquetas sobre los cubos**: `J0`, `J1`, ... (dueño) o `LIBRE` (sin dueño).

## 4. Comprobación por criterio

### Criterio 1 - Roles por código, sin duplicar jugadores, promoción
- Host: HUD **GUARDIÁN** dorado. Client: HUD **EXPLORADOR** cian.
- Jerarquía del host: **1 Player por cliente** (no duplicados).
- El cliente se mueve con WASD (su Player es suyo).
- Si el Guardián (no-host) se va, el siguiente cliente pasa a Guardián (log
  `[Lab6] El Guardian se desconecto; el cliente N pasa a ser Guardian`).

### Criterio 2 - Cofre por solicitud del Guardián
- Guardián pulsa **G** → aparece el cofre **delante de su personaje** (≈2,6 m), sobre la plataforma
  dorada y con luz cálida; mensaje `Cofre invocado por el Guardián`.
- Explorador pulsa **G** → mensaje `Solo el Guardián puede invocar el cofre` (no se crea).
- Si el Explorador se acerca al cofre y pulsa **Espacio** → el servidor rechaza y muestra
  `Solo el Guardián puede abrir el cofre` (solo el Guardián puede abrirlo).
- Segundo **G** → log `[Lab6] Solicitud de cofre ignorada: ya existe un cofre`.
- Acercarse y **Espacio** → se abre al instante (`TimeToOpenChest = 0`), suelta **5 cubos**, se ven
  **chispas doradas** y el mensaje `¡El cofre se ha abierto!`.

### Criterio 3 - El botín nace con la propiedad del que abre
- Tras abrir, en la jerarquía del host cada cubo tiene como **Owner** al cliente que abrió.
- Los 5 cubos tienen el **color del dueño** y la etiqueta `J<id>`.

### Criterio 4 - Transferencia al más cercano (validada por el servidor)
- Recoger un cubo (**Espacio** cerca, ≤2 m) → queda en la mano y sale `¡Botín obtenido!`.
- Pulsar **T** → pasa al **otro** jugador (cambia el Owner; **flash blanco**; mensaje
  `Cubo transferido al jugador X`; log `[Lab6] Botin N transferido al cliente X`).
- Sin otro jugador → log `[Lab6] No hay otro jugador al que transferir el botin`.

### Criterio 5 - Consumo + botín libre al desconectarse el dueño
- Recoger un cubo y pulsar **Q** → se **funde a negro** y desaparece (mensaje
  `Cubo consumido por el jugador X`; log `[Lab6] Botin N consumido...`).
- Un cliente dueño de un cubo se desconecta (**X**) → el cubo **no** se destruye, cae, queda **gris**
  con etiqueta **LIBRE** y sube el contador **Cubos libres en el suelo**.
- Otro jugador se acerca y pulsa **Espacio** → lo reclama (mensaje
  `Botín libre reclamado por el jugador X`; log `... reclamado por el cliente X`).

### Extra - Revisión visual
- El **cofre** se ve como un tesoro (madera, tapa, bandas doradas, cerradura y patas).
- El **escenario** tiene suelo verde, cuatro pilares y una luz cálida junto al cofre.
- Los **jugadores** se distinguen por color de rol y los **cubos** por color/etiqueta de dueño.
- Los **mensajes** se desvanecen y no tapan el HUD.

## 5. Estado de verificación

### Resumen de los cinco criterios (rellenar al ejecutar)

| # | Criterio | Visible en el juego | Estado |
|---|---|---|---|
| 1 | Roles + promoción | HUD `GUARDIÁN`/`EXPLORADOR` con color; 1 Player/cliente | **[PENDIENTE]** |
| 2 | Cofre sólo Guardián, delante, sin duplicados | Cofre sobre plataforma delante del Guardián; rechazo al Explorador | **[PENDIENTE]** |
| 3 | Botín del que abre | 5 cubos con color/etiqueta del dueño | **[PENDIENTE]** |
| 4 | Transferencia al más cercano | Flash + cambio de color/etiqueta + mensaje | **[PENDIENTE]** |
| 5 | Consumo y botín libre | Fundido al consumir; cubo gris `LIBRE` al desconectarse | **[PENDIENTE]** |
| - | Revisión visual | HUD, cofre, colores, etiquetas, efectos, mensajes | **[PENDIENTE]** |

### Verificaciones ya hechas (sin ejecutar partida)

| Elemento | Estado |
|----------|--------|
| Compilación de los scripts (MSBuild) tras la revisión visual | **[VERIFICADO]** 0 errores / 0 advertencias |
| GUIDs y fileIDs (escena, prefabs, lista de red) | **[VERIFICADO]** 0 roturas |
| APIs de NGO 2.13.2 usadas (incl. `SendTo.ClientsAndHost`, desparentado, `RemoveOwnership`) | **[VERIFICADO]** leídas del paquete |
| Criterios 1-5 en ejecución | **[PENDIENTE]** ejecutar esta guía |
| Revisión visual (HUD, cofre, colores, etiquetas, efectos, mensajes) | **[PENDIENTE]** |

**Archivos modificados en la revisión final**: `Lab6RoleManager.cs`, `PlayerAvatar.cs`,
`ColorCube.cs`, `Chest.cs` y `Documentacion/{INFORME_FINAL, PRUEBAS, REGISTRO_CAMBIOS,
GUIA_EJECUCION_PRUEBAS}_LAB6.md`. Sin nuevos assets ni GUIDs.

## 6. Notas

- `Chest.prefab` tiene `TimeToOpenChest = 0` → apertura instantánea (sin progreso visible). Para
  verlo, poner el valor a `3` en el prefab.
- La Game view de cada instancia debe tener el foco para recibir teclado.
- El cofre aparece delante del Guardián; si está pegado al borde de la plataforma, la posición se
  acota automáticamente.
- Cualquier ajuste manual: registrarlo en `Documentacion/REGISTRO_CAMBIOS_LAB6.md`.
