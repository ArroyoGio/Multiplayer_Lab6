# Registro de cambios - Laboratorio 6

Proyecto: `Multiplayer_Lab5` (Unity 6000.3.10f1, Netcode for GameObjects 2.13.2,
Input System 1.18.0)
Fecha: 19/10/2026

Todos los cambios se hicieron **por código / edición de datos** reutilizando la estructura del
Laboratorio 5. No se sustituyeron recursos ni se inventaron identificadores: los GUID/fileID nuevos
reutilizan los del objeto `Cofre` que ya existía en la escena. La **revisión visual final** no añade
assets ni GUIDs: los elementos decorativos se construyen en tiempo de ejecución.

---

## 1. Scripts nuevos

### `Assets/Scripts/Lab6RoleManager.cs` (NUEVO)
- GUID: `7b1c0f2d3e4a5b6c7d8e9f0a1b2c3d4e` (ver `Lab6RoleManager.cs.meta`).
- Enums: `PlayerRole : byte { None, Guardian, Explorer }` y `Lab6MsgKind : byte`.
- **Roles por código**:
  - `Initialize()` (desde `Start()` y reintento en `Update()`) fuerza
    `NetworkConfig.ConnectionApproval = true` y registra `ConnectionApprovalCallback` +
    `OnClientDisconnectCallback`.
  - Aprobación: **primer cliente = Guardián**, resto Explorador.
  - Al desconectarse el Guardián, **promueve al cliente conectado más antiguo**.
- **Cofre**: `SpawnChestForGuardian(clientId)` valida Guardián + `HasChest` y lo instancia/spawnea
  **delante del Guardián** (`posición + forward * 2.6 m`, acotado a la plataforma). Mueve con él la
  plataforma y la luz.
- **Arranque**: `HandleStartHotkeys` (H/C/X) y panel `OnGUI` con `Start Host`, `Start Client`,
  `Start Server` y `Shutdown`.
- **HUD**: título `EL COFRE DEL GUARDIÁN`, modo/estado/`ClientId`, rol con color, contadores de cubos
  propios y libres, lista de controles, botón de desconexión.
- **Mensajes**: `PostMessage` (local) + `Broadcast` (servidor -> `PlayerAvatar.NotifyRpc`,
  `SendTo.ClientsAndHost`), con lista temporizada y desvanecido.
- **Colores**: `RoleColor` (Guardián dorado, Explorador cian), `OwnerColor` (paleta por `clientId`)
  y `SetRendererColor` (escribe `color` y `_BaseColor` para URP).
- **Escenario**: tiñe el suelo, crea 4 pilares, una plataforma dorada y una luz cálida junto al
  cofre (todo local, sin objetos de red).
- **Etiquetas de cubos**: `OnGUI` + `WorldToScreenPoint` dibuja `J<id>` o `LIBRE` sobre cada cubo.

## 2. Scripts modificados

### `Assets/Scripts/PlayerAvatar.cs`
- `using UnityEngine.InputSystem;`.
- Nuevo `NetworkVariable<PlayerRole> Role` (Everyone/Server).
- `OnNetworkSpawn()`: en el servidor asigna el rol (`ResolveRole`); **se suscribe a
  `Role.OnValueChanged`** y aplica el tinte del material (`ApplyRoleVisual`) en todas las
  instancias. `OnNetworkDespawn()` se desuscribe.
- `SetRole(role)` (sólo servidor) para la promoción en caliente.
- `NotifyRpc(string, byte)` (`SendTo.ClientsAndHost`) para mostrar mensajes en todos los clientes.
- `Update()`: **G** -> `RequestChestRpc()` si es Guardián; si no, mensaje
  `Solo el Guardián puede invocar el cofre`.
- `TryInteract()`: reclama botín libre (`ClaimRpc`) y muestra `Reclamando botín libre...`.
- `UpdateCurrentCube()`: detecta botín libre cercano; al coger, muestra `¡Botín obtenido!`.
- Se conserva el resto del Laboratorio 5 (movimiento, lanzamiento, etc.).

### `Assets/Scripts/ColorCube.cs`
- Migrado a **Input System** (antes `Input` legacy, desactivado en el proyecto).
- Nuevo `NetworkVariable<bool> IsFreeLoot` (Everyone/Server).
- **Color por propietario**: `RefreshColor` usa `Lab6RoleManager.OwnerColor(OwnerClientId)`; gris
  (`FreeLootColor`) si es libre. Se llama en `OnNetworkSpawn`, en `OnFreeLootChanged` y en
  `OnOwnershipChanged` **en todas las instancias** (antes sólo servidor).
- `OnOwnershipChanged()`: **flash blanco** al cambiar de dueño; en el servidor, si la propiedad pasa
  al servidor marca `IsFreeLoot = true` (quita kinematic, activa gravedad) y si pasa a un cliente lo
  desmarca.
- Nuevas teclas: **T** -> `TransferToNearestPlayerRpc()`, **Q** -> `ConsumeRpc()`.
- **Transferencia**: valida emisor = dueño, busca el `PlayerAvatar` más cercano distinto, suelta el
  cubo y `ChangeOwnership`; avisa `Cubo transferido al jugador X`; aviso local si no hay otro.
- **Consumo**: valida, avisa `Cubo consumido por el jugador X`, ejecuta un **fundido a negro**
  (`PlayConsumeFxRpc`, 0,3 s) y hace `Despawn(true)` tras 0,3 s para que llegue el efecto.
- **Reclamo**: `ClaimRpc` (Everyone) valida `IsFreeLoot`, cambia la propiedad al emisor y avisa
  `Botín libre reclamado por el jugador X`.

### `Assets/Scripts/Chest.cs`
- `Awake()` construye el **aspecto de tesoro** con primitivas: tapa, dos bandas doradas, cerradura y
  cuatro patas (colliders desactivados/destruidos, materiales URP por instancia). Sin assets nuevos.
- `OnNetworkDespawn()` lanza una **explosión local de chispas doradas** (sólo si no se está
  apagando la sesión).
- `OpenChest()`: 5 cubos con `SpawnWithOwnership(ownerClientId)` (propiedad del que abre), aviso
  `¡El cofre se ha abierto!` y despawn del cofre.
- `TimeToOpenChest` se usa con `Mathf.Max(..., 0.0001f)` para evitar división entre cero.
- La lógica de apertura/progreso (`StartOpeningChestRpc`, `CancelOpeningChestRpc`, `UserOpening`,
  `Progress`) se conserva.

## 3. Prefabs

### `Assets/Prefab/Chest.prefab` (NUEVO)
- Meta GUID: `9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b`.
- Reproduce el objeto `Cofre` de la escena con los **mismos fileID**:
  - GameObject `225163045`, Transform `225163049`, MeshFilter `225163048`, MeshRenderer `225163047`
    (URP Lit `31321ba15b8f8eb4c954353edc038b1d`), BoxCollider `225163046`,
    NetworkObject `225163050` (`GlobalObjectIdHash: 3742552873`, `m_InScenePlaced: 0`),
    Chest `225163051` (`cubePrefab` -> ItemGOZU).
  - `TimeToOpenChest: 0` (apertura inmediata; valor heredado).

### `Assets/Prefab/Player.prefab`
- `m_InScenePlaced: 1 -> 0` (ahora es `PlayerPrefab` de red).
- `NetworkTransform.AuthorityMode: 0 -> 1` (**Owner**): sin esto el cliente no podía mover su
  jugador.
- `GlobalObjectIdHash: 1173258463` (sin cambios).

### `Assets/Prefab/ItemGOZU.prefab`
- `DontDestroyWithOwner: 0 -> 1`: al desconectarse el dueño, Netcode no destruye el cubo
  (Requisito 7).
- `AllowOwnerToParent: 0 -> 1`: el dueño puede emparentarlo a su jugador (cogerlo).
- `AuthorityMode: 0 -> 1` (**Owner**) y `SwitchTransformSpaceWhenParented: 0 -> 1`: movimiento y
  emparentado correctos para el dueño.
- `GlobalObjectIdHash: 120291310` (sin cambios).

## 4. Lista de prefabs de red

### `Assets/DefaultNetworkPrefabs.asset`
- Tercera entrada para el cofre:
  `Prefab: {fileID: 225163045, guid: 9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b, type: 3}`.
  (Se conservan ItemGOZU y Player.)

## 5. Escena `Assets/Scenes/SampleScene.unity`

Pasó de **899** a **682** líneas.

- **Eliminado el objeto `Cofre`** (bloque `&225163045` y sus 6 componentes): ahora lo crea el
  Guardián por solicitud. Sus fileID se reutilizan en `Chest.prefab`.
- **Eliminada la instancia de `Player`** (`PrefabInstance &1996777240`) y sus raíces; quedan 5
  raíces (Main Camera, Directional Light, Global Volume, NetworkManager, Plane).
- **`NetworkManager`** (`GameObject &1703080215`):
  - `PlayerPrefab: {fileID: 0}` -> `Player.prefab` (Netcode crea un jugador por cliente).
  - `ConnectionApproval: 0 -> 1`.
  - Componente `Lab6RoleManager` (`1703080219`) con `chestPrefab`, `chestSpawnPosition`
    (`4.22, 0, 0.70711`), `playerSpawnOrigin` (`1.7906, 0.15, 3.62181`), `playerSpawnSpacing: 2`,
    `showHud: 1`.
- **La revisión visual no modifica la escena**: los nuevos campos de `Lab6RoleManager` son privados
  (no serializados) y los públicos conservan sus valores.

## 6. Correcciones tras la primera prueba de ejecución

Documentadas en `CORRECCIONES_PRIMERA_PRUEBA_LAB6.md`:
1. `Chest.OnNetworkSpawn` escribía `Progress` en clientes -> se limitó a `if (IsServer)`.
2. El cliente no se movía -> `Player.prefab` `NetworkTransform.AuthorityMode` a **Owner**.
3. El dueño no podía coger/lanzar el botín -> `ItemGOZU.prefab` `AllowOwnerToParent`,
   `AuthorityMode` y `SwitchTransformSpaceWhenParented` a **Owner/1**.
4. Sin forma práctica de arrancar -> panel H/C/X en `Lab6RoleManager`.

## 7. Estado de los cinco criterios

| # | Criterio | Implementación | Estado estático | Ejecución |
|---|---|---|---|---|
| 1 | Roles (Guardián/Explorador, aprobación, desconexión) | `OnConnectionApproval`, `PlayerAvatar.Role`, `ApplyRole` + promoción | **CUMPLE** | **PENDIENTE** |
| 2 | Cofre (sólo Guardián, delante, sincronizado, sin duplicados) | `SpawnChestForGuardian` valida + `HasChest` + posición delante | **CUMPLE** | **PENDIENTE** |
| 3 | Botín (5 cubos, propiedad del que abre) | `SpawnWithOwnership(opener)` + color/etiqueta | **CUMPLE** | **PENDIENTE** |
| 4 | Transferencia (al más cercano, visible en red) | `TransferToNearestPlayerRpc` + `ChangeOwnership` + flash | **CUMPLE** | **PENDIENTE** |
| 5 | Consumo y desconexión (botín libre) | `ConsumeRpc` + `DontDestroyWithOwner` + `RemoveOwnership` | **CUMPLE** | **PENDIENTE** |

**Archivos modificados en la revisión final**: `Lab6RoleManager.cs`, `PlayerAvatar.cs`,
`ColorCube.cs`, `Chest.cs` y los cuatro documentos de `Documentacion/`. **Sin nuevos assets ni
GUIDs.**

## Verificaciones realizadas

| Verificación | Resultado |
|---|---|
| Compilación de todos los `.cs` (MSBuild, `LangVersion 9.0`) tras la revisión final | **0 errores, 0 advertencias** |
| Referencias `fileID` locales de la escena | **0 rotas** |
| Referencias `guid` de escena + prefabs + lista de prefabs | **0 rotas** |
| IDs únicos en la escena y en `Chest.prefab` | OK (sin duplicados) |
| APIs verificadas contra el código del paquete 2.13.2 (incl. `SendTo.ClientsAndHost`, desparentado al destruir, `RemoveOwnership`) | ver `INFORME_FINAL_LAB6.md` |
| Partidas multijugador | **NO EJECUTADAS** (ver `PRUEBAS_LAB6.md`) |

> Nota: `Multiplayer_lab2` (otro proyecto) **no** se tocó.
