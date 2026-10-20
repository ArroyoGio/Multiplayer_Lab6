# Informe final - Laboratorio 6

Proyecto: `Multiplayer_Lab5`
Entorno: Unity `6000.3.10f1` · Netcode for GameObjects `2.13.2` · Input System `1.18.0` ·
Universal RP `17.3.0`
Fecha: 19/10/2026

---

## 1. Objetivo

Sobre la base **funcional del Laboratorio 5** (movimiento, interacciones, física, RPC y configuración
de red ya existentes), añadir el sistema de **roles y botín** del Laboratorio 6:

- activar los roles **por código** y **evitar la creación duplicada de jugadores**;
- que el **cofre** lo cree el **Guardián por solicitud**, delante de su personaje;
- que el **botín nazca con la propiedad de red** del jugador que abre el cofre;
- **transferir** el botín al **jugador más cercano** con validación del servidor;
- **consumir** cubos y, al **desconectarse el dueño**, dejar el cubo en el mundo como **botín libre**.

Además, en la revisión final se hizo una **mejora visual** para que todos los criterios se puedan
comprobar de un vistazo: HUD con estado/rol/contadores, cofre con aspecto de tesoro, colores y
etiquetas de propietario en los cubos, mensajes temporales y efectos de transferencia/consumo.

## 2. Diagnóstico previo (por qué se cambió lo que se cambió)

La inspección de `SampleScene.unity` y los prefabs reveló el estado real del Laboratorio 5:

1. **Había un `Player` colocado dentro de la escena** (`PrefabInstance &1996777240`,
   `m_InScenePlaced: 1`, `GlobalObjectIdHash: 902557273`). Un objeto "colocado en escena" por el
   servidor no es propiedad de los clientes, por lo que **sólo el host podía mover/jugar** con él.
2. **`NetworkConfig.PlayerPrefab` estaba vacío** (`{fileID: 0}`): Netcode **no** creaba un jugador
   por cliente.
3. Consecuencia: activar roles para dos jugadores reales exige **un jugador por cliente**. Por eso:
   se fijó `PlayerPrefab`, se marcó `Player.prefab` como `m_InScenePlaced: 0` y **se eliminó la
   instancia de Player de la escena**. Si se hubiera dejado, el host tendría **dos** jugadores (el
   duplicado que el enunciado pide evitar).
4. **El `Cofre` estaba en la escena.** Como el Laboratorio 6 pide que **nazca por solicitud del
   Guardián**, se quitó de la escena y su contenido exacto (mismos fileID) pasó a `Chest.prefab`
   registrado en las listas de prefabs de red.
5. `ColorCube.LateUpdate` usaba `Input` **legacy**, desactivado en este proyecto
   (`activeInputHandler: 1`); se adaptó al **Input System** porque de lo contrario lanzaría excepción
   (adaptación estrictamente necesaria).

## 3. Diseño implementado

### 3.1 Activación y reparto de roles por código (Requisito 3)
- `Lab6RoleManager` (nuevo) fuerza `ConnectionApproval = true` y registra el
  `ConnectionApprovalCallback` **antes** de arrancar (`Start()` + reintento en `Update()`).
- En la aprobación: **primer cliente = Guardián**, resto **Explorador**. El rol se replica en
  `PlayerAvatar.Role` (`NetworkVariable<PlayerRole>`).
- **Sin duplicados**: los jugadores los crea Netcode **una sola vez por cliente**
  (`response.CreatePlayerObject = true` + `PlayerPrefab` fijado + `Player` de escena eliminado).
  No se instancian jugadores a mano.
- Al desconectarse el Guardián se **promueve al cliente conectado más antiguo**
  (`OnClientDisconnect` + `ApplyRole` sobre su `PlayerObject`).

### 3.2 Cofre por solicitud del Guardián (Requisito 4)
- Tecla **G** -> `PlayerAvatar.RequestChestRpc()` -> `Lab6RoleManager.SpawnChestForGuardian()`.
- El servidor **valida que el solicitante sea el Guardián** y que **no exista ya** un cofre; luego
  calcula la posición **delante del Guardián** (`posición + forward * 2.6 m`, acotada a la
  plataforma), mueve allí la **plataforma** y la **luz** y hace `Instantiate` + `Spawn()` del
  `Chest.prefab`.
- La **apertura y el progreso** siguen siendo los del Laboratorio 5, sin tocar
  (`StartOpeningChestRpc`/`CancelOpeningChestRpc`/`Progress`).

### 3.3 Botín con la propiedad de quien abre (Requisito 5)
- `Chest.OpenChest()` usa `colorCube.SpawnWithOwnership(ownerClientId)` (antes `Spawn()` genérico),
  de forma que **cada cubo nace siendo propiedad del jugador que abrió el cofre**.
- Cada cubo se pinta con un **color estable por propietario** (`clientId % 6`) y muestra una
  **etiqueta** en pantalla (`J0`, `J1`, ... o `LIBRE`).

### 3.4 Transferencia al jugador más cercano (Requisito 6)
- Tecla **T** -> `ColorCube.TransferToNearestPlayerRpc()` (`SendTo.Server`,
  `InvokePermission = Owner`).
- El **servidor** comprueba que el emisor es el dueño, busca el `PlayerAvatar` **más cercano distinto
  del emisor**, suelta el cubo de la mano (`TryRemoveParent`) y llama a `ChangeOwnership`.
- Todos los clientes ven el cambio porque `OnOwnershipChanged` se ejecuta en **todas** las
  instancias: se repinta el color/etiqueta y se produce un **flash blanco** de confirmación.

### 3.5 Consumo y botín libre persistente (Requisito 7)
- Tecla **Q** -> `ColorCube.ConsumeRpc()`: el servidor valida, avisa a todos y hace un **fundido a
  negro** de 0,3 s antes de `Despawn(true)`.
- En `ItemGOZU.prefab` se puso `DontDestroyWithOwner: 1`: al desconectarse el dueño Netcode **no**
  destruye el cubo y le quita la propiedad (`RemoveOwnership` -> propietario servidor).
  `ColorCube.OnOwnershipChanged` detecta el paso al servidor, marca `IsFreeLoot = true` (gris) y lo
  deja como botín libre. Si el cubo estaba **en la mano**, Netcode lo **desemparenta** antes de
  destruir al jugador (`NetworkSpawnManager.OnDespawnObject`, líneas 1724-1760), así que también
  sobrevive.
- Cualquier jugador cercano lo **reclama** con **Espacio** (`ClaimRpc`, validado en el servidor).

### 3.6 Controles y HUD

| Tecla | Acción |
|---|---|
| Acción `Move` (WASD) | Moverse (Laboratorio 5) |
| **Espacio** | Interactuar: abrir/cancelar cofre, coger cubo, **reclamar botín libre** |
| **E** o clic derecho | Lanzar el cubo sostenido (Laboratorio 5) |
| **G** | Pedir la creación del cofre (sólo Guardián) |
| **T** | Transferir el botín sostenido al jugador más cercano |
| **Q** | Consumir el botín sostenido |
| **H / C / X** | Start Host / Start Client / Shutdown |

HUD (`OnGUI` del `Lab6RoleManager`):
- **antes de conectar**: panel de arranque con `Start Host (H)`, `Start Client (C)`, `Start Server`
  y la indicación de usar Multiplayer Play Mode;
- **durante la partida**: título **EL COFRE DEL GUARDIÁN**, modo (`HOST`/`SERVIDOR`/`CLIENTE`),
  estado de conexión, `ClientId`, **rol con color** (Guardián dorado, Explorador cian),
  **contadores de cubos propios y libres**, lista de controles y botón `Shutdown (X)`;
- **mensajes temporales** (abajo, se desvanecen): cofre invocado, cofre abierto, botín obtenido,
  cubo transferido, cubo consumido, botín libre reclamado, rechazos y desconexión/promoción.

### 3.7 Mejoras visuales de la revisión final

- **HUD**: panel oscuro semitransparente, título, estado de conexión, rol coloreado, contadores y
  controles.
- **Cofre**: construido como tesoro (cuerpo de madera, tapa, dos bandas doradas, cerradura y patas)
  a partir de primitivas en `Awake`, sin materiales ni assets nuevos; **plataforma** dorada y **luz
  cálida** que se colocan donde aparece el cofre; **explosión de chispas doradas** al abrirse.
- **Cubos**: color por propietario, etiqueta `J<id>`/`LIBRE` sobre el cubo, **flash** al cambiar de
  dueño y **fundido** al consumirse.
- **Jugadores**: tinte del material según el rol (dorado/cian) en todas las instancias.
- **Escenario**: suelo teñido de verde, cuatro pilares de piedra y luz de ambiente junto al cofre.
- Todos los efectos son **locales** (no crean objetos de red), por lo que no alteran la
  sincronización ni duplican nada.

## 4. Archivos

**Nuevos**
- `Assets/Scripts/Lab6RoleManager.cs` (+ `.meta`, guid `7b1c0f2d3e4a5b6c7d8e9f0a1b2c3d4e`)
- `Assets/Prefab/Chest.prefab` (+ `.meta`, guid `9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b`)
- `Documentacion/{INFORME_FINAL, PRUEBAS, REGISTRO_CAMBIOS, GUIA_EJECUCION_PRUEBAS,
  CORRECCIONES_PRIMERA_PRUEBA}_LAB6.md`

**Modificados en la revisión final**
- `Assets/Scripts/Lab6RoleManager.cs` (HUD, mensajes, difusión, colores, escenario, cofre delante)
- `Assets/Scripts/PlayerAvatar.cs` (tinte por rol, `NotifyRpc`, avisos)
- `Assets/Scripts/ColorCube.cs` (color/etiqueta por dueño, flash, fundido, mensajes)
- `Assets/Scripts/Chest.cs` (visual de tesoro, chispas, aviso de apertura, división segura)

**Modificados en la fase funcional anterior**
- `Assets/Prefab/Player.prefab` (`m_InScenePlaced: 1 -> 0`, `NetworkTransform.AuthorityMode -> Owner`)
- `Assets/Prefab/ItemGOZU.prefab` (`DontDestroyWithOwner: 1`, `AllowOwnerToParent: 1`,
  `AuthorityMode: Owner`, `SwitchTransformSpaceWhenParented: 1`)
- `Assets/DefaultNetworkPrefabs.asset` (entrada del cofre)
- `Assets/Scenes/SampleScene.unity` (ver `REGISTRO_CAMBIOS_LAB6.md`)

**Sin tocar**: el proyecto `Multiplayer_lab2`.

## 5. Verificación de APIs contra la versión instalada (NGO 2.13.2)

Se comprobó el **código real del paquete** antes de escribir, corrigiendo suposiciones:

| Suposición | Realidad en 2.13.2 | Acción |
|---|---|---|
| `request.SenderClientId` | El campo es **`ClientNetworkId`** (`NetworkManager.cs:845-855`) | **Corregido** |
| `NetworkVariable<enum>` necesitaba inicializador manual | El ILPP genera el serializador para enums (`NetworkBehaviourILPP.cs:251/310/359`) | Sin cambios |
| `AutoSpawnPlayerPrefabClientSide` podía duplicar jugadores | Sólo aplica en **DistributedAuthority**; en cliente-servidor es inocuo | Sin riesgo |
| `response.Position/Rotation` sólo en DA | El servidor los respeta en cliente-servidor (`NetworkConnectionManager.cs:1014`) | Usados para repartir apariciones |
| `SendTo.ClientsAndHost` | Existe en `Runtime/Messaging/RpcTargets/RpcTarget.cs` | Usado para difundir mensajes |
| Un cubo emparentado muere con el jugador | NGO **desemparenta** los hijos antes de destruir al padre (`NetworkSpawnManager.cs:1724-1760`) | El botín libre sobrevive |
| `RemoveOwnership` deja el objeto al servidor | `ChangeOwnership(..., ServerClientId, true)` (`NetworkSpawnManager.cs:457-468`) | `OnOwnershipChanged` marca libre |

## 6. Auditoría de los cinco criterios

Estado **estático** = revisado en código/paquete y compilado. **Ejecución** = partida real
Host+cliente, que **no** se pudo ejecutar en este entorno.

| # | Criterio | Implementación / evidencia | Estado estático | Ejecución |
|---|---|---|---|---|
| 1 | **Roles** (Guardián/Explorador, aprobación, prefabs, desconexión del Guardián) | `OnConnectionApproval` (primer cliente = Guardián), `PlayerAvatar.Role`, `ResolveRole`, `ApplyRole`, promoción en `OnClientDisconnect`; un solo `Player.prefab` con tinte por rol | **CUMPLE** | **PENDIENTE** |
| 2 | **Cofre** (sólo Guardián, delante de su personaje, sincronizado, sin duplicados) | `RequestChestRpc` -> `SpawnChestForGuardian` valida Guardián + `HasChest`; posición `guardián + forward*2.6 m`; `Instantiate`+`Spawn` en servidor | **CUMPLE** | **PENDIENTE** |
| 3 | **Botín** (5 cubos, propiedad del que abre, sincronizada y visible) | `OpenChest` crea 5 cubos con `SpawnWithOwnership(opener)`; `IsFreeLoot` y color/etiqueta replicados | **CUMPLE** | **PENDIENTE** |
| 4 | **Transferencia** (al más cercano, propiedad en red, visible en ambos clientes) | `TransferToNearestPlayerRpc` (Owner) valida y hace `ChangeOwnership`; `OnOwnershipChanged` repinta y hace flash en todas las instancias | **CUMPLE** | **PENDIENTE** |
| 5 | **Consumo y desconexión** (desaparece; el botín del dueño desconectado queda libre) | `ConsumeRpc` + fundido + `Despawn`; `DontDestroyWithOwner=1` + `RemoveOwnership` + `IsFreeLoot` + `ClaimRpc` | **CUMPLE** | **PENDIENTE** |

> **Nota sobre el criterio 1**: se usa un **único `Player.prefab`** (el del Laboratorio 5) y la
> distinción de rol es visual (dorado/cian). No se crean dos prefabs para no duplicar assets ni IDs;
> si el enunciado exige prefabs separados por rol, es el único punto a ampliar.

> **Declaración de honestidad**: la compilación y las verificaciones anteriores son reales; **no** se
> ejecutaron partidas multijugador desde este entorno (la herramienta apunta a otro proyecto). No hay
> capturas ni logs de juego. La guía `GUIA_EJECUCION_PRUEBAS_LAB6.md` permite cerrar los criterios en
> 10-15 minutos.

## 7. Estado de las verificaciones

| Verificación | Estado |
|---|---|
| Compilación completa (MSBuild, LangVersion 9.0) tras la revisión final | **Hecha**: 0 errores, 0 advertencias |
| Referencias `guid`/`fileID` (escena, prefabs, lista de prefabs) | **Hecha**: 0 rotas |
| IDs únicos / estructura YAML coherente | **Hecha**: sin duplicados |
| APIs de NGO 2.13.2 (incl. desparentado y `RemoveOwnership`) | **Hecha**: leídas del paquete |
| **Partidas multijugador (Host + cliente)** | **NO EJECUTADA** |

## 8. Decisiones tomadas (y por qué)

- **`PlayerPrefab` + eliminar el Player de escena** en lugar de instanciar jugadores a mano: la vía
  natural (y la que menos código añade) es la creación automática de Netcode, que es exactamente la
  que el enunciado pide **no duplicar**.
- **Panel de arranque en pantalla (H/C/X)**: se añadió en la revisión final para poder verificar sin
  depender del Inspector; no cambia la lógica de red (`StartHost/StartClient/StartServer/Shutdown`).
- **Cofre delante del Guardián** en lugar de una posición fija: cumple el criterio al pie de la
  letra; la plataforma y la luz se mueven con él.
- **Cofre con los mismos fileID** que el de la escena: cumple "no sustituir recursos" y "no inventar
  identificadores".
- **`TimeToOpenChest = 0` conservado**: es el valor configurado en el Laboratorio 5; se documenta que
  hace que el cofre se abra de inmediato (con `Mathf.Max` para evitar dividir entre cero).
- **Efectos visuales locales** (chispas, flash, fundido, etiquetas): no crean `NetworkObject` ni
  alteran la sincronización.
- **Sólo el Guardián puede *pedir* el cofre**; la apertura/progreso se conservó tal cual.
