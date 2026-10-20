# Plan y estado de pruebas - Laboratorio 6

Proyecto: `Multiplayer_Lab5` (Unity 6000.3.10f1, NGO 2.13.2)

> **ESTADO GLOBAL: las pruebas de ejecución (multijugador) NO se han ejecutado.**
> No fue posible lanzar el editor/proyecto desde el entorno de trabajo automatizado
> (la herramienta apunta a otro proyecto), por lo que **no** hay capturas ni registros de
> partidas reales. Lo que sí se verificó está marcado como tal en cada bloque
> (`[VERIFICADO]` = comprobación estática real) frente a `[PENDIENTE]` (hay que jugarlo).
>
> Equivalencia de numeración: **Criterio 1 = Requisito 3**, **Criterio 2 = Requisito 4**,
> **Criterio 3 = Requisito 5**, **Criterio 4 = Requisito 6**, **Criterio 5 = Requisito 7**.

## 0. Verificaciones automáticas ya realizadas `[VERIFICADO]`

| # | Verificación | Método | Resultado |
|---|---|---|---|
| 0.1 | Compila todo `Assembly-CSharp` con los cambios (incluida la revisión visual) | MSBuild `/t:Rebuild`, LangVersion 9.0, v4.7.1 | 0 errores / 0 advertencias |
| 0.2 | `ConnectionApprovalRequest.ClientNetworkId` es el nombre real del campo | lectura de `NetworkManager.cs:845-855` (2.13.2) | correcto |
| 0.3 | En cliente-servidor el servidor respeta `CreatePlayerObject` y `Position/Rotation` | `NetworkConnectionManager.cs:1012-1014`, `NetworkManager.cs:1507-1516` | correcto |
| 0.4 | `AutoSpawnPlayerPrefabClientSide` no afecta en cliente-servidor | usos en `NetworkConnectionManager.cs:1092`, `ConnectionApprovedMessage.cs:350/383` | sin duplicados |
| 0.5 | `NetworkVariable<PlayerRole>` (enum) tiene serializador automático | `NetworkBehaviourILPP.cs:251/310/359` | correcto |
| 0.6 | `GetComponent<NetworkRigidbody>()` válido en el cubo | `ItemGOZU.prefab` tiene `NetworkRigidbody` | correcto |
| 0.7 | `SendTo.ClientsAndHost` existe | `Runtime/Messaging/RpcTargets/RpcTarget.cs` | correcto |
| 0.8 | Un cubo **en la mano** no muere con el jugador: NGO desemparenta los hijos antes de destruir el padre | `NetworkSpawnManager.cs:1724-1760` | el botín libre sobrevive |
| 0.9 | `RemoveOwnership` deja el objeto al servidor | `NetworkSpawnManager.cs:457-468` -> `ChangeOwnership(..., ServerClientId, true)` | activa `OnOwnershipChanged` |
| 0.10 | Referencias `guid`/`fileID` de escena, `Chest.prefab` y lista de prefabs | comprobación cruzada | 0 rotas |

## 1. Preparación del entorno

1. Abrir el proyecto en Unity **6000.3.10f1**. Si el editor ya estaba abierto al editar los
   archivos, aceptar **recargar** la escena `SampleScene`.
2. Confirmar que no hay errores de compilación en la consola.
3. Abrir `Assets/Scenes/SampleScene.unity` y verificar en el Inspector del `NetworkManager`:
   - `Player Prefab` = `Player`
   - `Connection Approval` = activado
   - Componente **Lab6RoleManager** con `Chest Prefab` = `Chest`, `Show Hud` = activado
4. Comprobar que **ya no** hay un `Cofre` ni un `Player` en la jerarquía de la escena.
5. Arrancar **dos instancias** con **Multiplayer Play Mode** (Window > Play Mode > Scenarios >
   Configure play mode scenarios > 1 Additional Editor Instance) o build + editor. En Play aparece
   el panel **EL COFRE DEL GUARDIÁN** con `Start Host (H)` / `Start Client (C)`.

## 2. Casos de prueba manuales

### CP-01 Criterio 1 - Rol asignado por código
Pasos:
1. En el editor principal, **Start Host (H)**. Esperado: aparece un jugador y el HUD muestra
   `Rol: GUARDIÁN` en **dorado**.
2. En el editor adicional (foco con **Ctrl+F10**), **Start Client (C)**. Esperado: el HUD del
   segundo muestra `Rol: EXPLORADOR` en **cian**.
`[PENDIENTE]`
Verificación indirecta `[VERIFICADO]`: asignación en `ConnectionApprovalCallback` (primer
cliente = Guardián) + respaldo `ResolveRole`; el rol viaja en `NetworkVariable<PlayerRole>`.

### CP-02 Criterio 1 - Sin jugador duplicado
Pasos: con 2 instancias, contar los `Player` en la jerarquía de cada instancia.
Esperado: **1 por cliente** (ningún cliente ve 2 jugadores propios).
`[PENDIENTE]`
Verificación indirecta `[VERIFICADO]`: se eliminó el `Player` de escena y se fijó `PlayerPrefab`;
Netcode crea un jugador por cliente.

### CP-03 Criterio 2 - Cofre: sólo Guardián, delante, sin duplicados
Pasos:
1. **G** en el Explorador. Esperado: **no** se crea cofre; aparece el mensaje
   `Solo el Guardián puede invocar el cofre` y el servidor registra el rechazo.
2. **G** en el Guardián. Esperado: aparece el cofre **delante de su personaje** (≈2,6 m), sobre la
   plataforma dorada y con luz cálida; mensaje `Cofre invocado por el Guardián`.
3. **G** otra vez en el Guardián. Esperado: no se crea un segundo cofre
   (`[Lab6] Solicitud de cofre ignorada: ya existe un cofre`).
4. Acercarse y **Espacio**. Esperado: el cofre se abre al instante (`TimeToOpenChest = 0`), suelta
   **5 cubos**, emite **chispas doradas** y muestra `¡El cofre se ha abierto!`.
`[PENDIENTE]`

### CP-04 Criterio 3 - Botín con la propiedad del que abre
Pasos: tras abrir, en la jerarquía del host cada cubo tiene como **Owner** al cliente que abrió el
cofre. Esperado: 5 cubos, todos del mismo dueño, con **color por dueño** y etiqueta `J<id>`.
`[PENDIENTE]`

### CP-05 Criterio 4 - Transferencia al más cercano
Pasos: con el Guardián sosteniendo un cubo, colocar al Explorador cerca y pulsar **T**.
Esperado: el cubo pasa a ser propiedad del Explorador (cambia el **Owner**, el color/etiqueta y hay
un **flash blanco**), y todos ven `Cubo transferido al jugador X`; el servidor registra
`[Lab6] Botin ... transferido al cliente ...`.
`[PENDIENTE]`

### CP-06 Criterio 4 - Transferencia rechazada sin otro jugador
Pasos: pulsar **T** estando solo. Esperado: no cambia nada y se registra
`[Lab6] No hay otro jugador al que transferir el botin`.
`[PENDIENTE]`

### CP-07 Criterio 5 - Consumo
Pasos: sostener un cubo y pulsar **Q**. Esperado: el cubo se **funde a negro** (0,3 s) y desaparece
del mundo para todos; mensaje `Cubo consumido por el jugador X`.
`[PENDIENTE]`

### CP-08 Criterio 5 - Botín libre al desconectarse el dueño
Pasos:
1. El Explorador coge un cubo (es su dueño y está **en la mano**).
2. Cerrar el Explorador (**X**). Esperado: el cubo **no desaparece**, cae, queda **gris** con
   etiqueta **LIBRE** y sube el contador de cubos libres.
3. El Guardián se acerca y pulsa **Espacio**. Esperado: lo **reclama** (pasa a ser suyo) y se ve
   `Botín libre reclamado por el jugador X`.
`[PENDIENTE]` (`[VERIFICADO]` estático: NGO desemparenta al hijo antes de destruir al jugador y
`RemoveOwnership` lo deja al servidor -> `IsFreeLoot`).

### CP-09 Criterio 1 - Promoción de Guardián
Pasos: cerrar al Guardián teniendo un Explorador conectado. Esperado: el Explorador pasa a
**Guardián** (HUD dorado + log `[Lab6] El Guardian se desconecto; ...`) y ya puede pedir el cofre
con **G**.
`[PENDIENTE]`

### CP-10 Conservación del Laboratorio 5
Pasos: mover con WASD; coger (**Espacio**) y lanzar (**E** o clic derecho) cubos.
Esperado: la mecánica del Laboratorio 5 sigue funcionando.
`[PENDIENTE]` (`[VERIFICADO]`: la lógica de movimiento/agarre/lanzamiento no se modificó; sólo se
adaptó la lectura de teclas al Input System.)

### CP-11 Revisión visual (transversal)
Pasos y esperado:
1. **HUD**: título `EL COFRE DEL GUARDIÁN`, modo (`HOST`/`SERVIDOR`/`CLIENTE`), estado de conexión,
   `ClientId`, rol con color, contadores de cubos propios/libres y lista de controles. `[PENDIENTE]`
2. **Mensajes**: aparecen abajo, se desvanecen y no tapan el HUD; se ven los de cofre, botín,
   transferencia, consumo, reclamo y desconexión. `[PENDIENTE]`
3. **Cofre**: madera + bandas doradas + cerradura + patas; plataforma y luz; chispas al abrir.
   `[PENDIENTE]`
4. **Cubos**: color por dueño, etiqueta `J<id>`/`LIBRE` sobre cada cubo, flash al transferir y
   fundido al consumir. `[PENDIENTE]`
5. **Jugadores/escenario**: Guardián dorado, Explorador cian; suelo verde, pilares y luz. `[PENDIENTE]`

## 3. Resumen de estado

| # | Criterio | Implementado | Verificado estáticamente | Probado en juego |
|---|---|---|---|---|
| 1 | Roles por código, sin duplicar jugadores, promoción | Sí | Sí | **No** |
| 2 | Cofre sólo Guardián, delante, sincronizado, sin duplicados | Sí | Sí | **No** |
| 3 | Botín nace con propiedad del que abre | Sí | Sí | **No** |
| 4 | Transferencia al más cercano (validada por servidor) | Sí | Sí | **No** |
| 5 | Consumo + botín libre persistente | Sí | Sí | **No** |
| - | Mejora visual (HUD, cofre, colores, etiquetas, efectos, mensajes) | Sí | Sí (compila) | **No** |
| - | Se conserva el Laboratorio 5 | Sí | Sí | **No** |

**Archivos modificados en la revisión final**: `Lab6RoleManager.cs`, `PlayerAvatar.cs`,
`ColorCube.cs`, `Chest.cs` y los cuatro documentos de `Documentacion/`. Sin nuevos assets ni GUIDs.

## 4. Riesgos / cosas a vigilar al probar

- **Foco de teclado**: cada instancia de MPPM debe tener el foco (Ctrl+F10) para recibir WASD/G/T/Q.
- **Arranque**: usar el panel en pantalla o los botones del Inspector; el callback de aprobación se
  registra en `Start()` (antes de que puedas pulsar). Si arrancas desde código muy pronto, el
  respaldo `ResolveRole` sigue asignando roles.
- **`TimeToOpenChest = 0`**: apertura instantánea (heredado). Para ver progreso gradual, poner un
  valor > 0 en `Chest.prefab`.
- **`Camera.main`**: debe existir una cámara etiquetada `MainCamera` (la escena la tiene).
- **Cofre delante**: si el Guardián está pegado al borde de la plataforma, la posición se acota y el
  cofre puede quedar algo desplazado; no afecta a la lógica.
- Si el editor estaba abierto durante la edición de la escena, elegir **Reload** para no guardar una
  versión antigua encima de los cambios.
