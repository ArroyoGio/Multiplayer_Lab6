# Correcciones tras la primera prueba en runtime - Laboratorio 6

## 1. Error de permisos al crear el cofre en el cliente

- **Sintoma:** `|Client-1|Chest(Clone)|Progress| Write permissions (Server) for this client instance is not allowed!`
  en `Chest.OnNetworkSpawn` (`Chest.cs:17`).
- **Causa:** `OnNetworkSpawn()` escribia `Progress.Value = 0f;` en todas las instancias, pero `Progress`
  es de escritura exclusiva del servidor.
- **Correccion:** la escritura se envolvio en `if (IsServer)`.

## 2. El cliente no se movia

- **Causa:** el `NetworkTransform` de `Player.prefab` tenia `AuthorityMode: 0` (**Server**). El host no es
  dueno del jugador del cliente (`PlayerAvatar.Update` sale si `!IsOwner`), asi que nadie movia ese
  transform en el servidor; el `NetworkTransform` no-autoritativo del cliente interpolaba de vuelta a la
  posicion de aparicion, anulando el movimiento local.
- **Correccion:** `AuthorityMode: 0 -> 1` (**Owner**) en `Assets/Prefab/Player.prefab`.

## 3. El botin no se podia agarrar/lanzar/transferir (cliente dueno)

En `Assets/Prefab/ItemGOZU.prefab` (el botin):

- `NetworkObject.AllowOwnerToParent: 0 -> 1`: sin esto, el cliente dueno **no puede** emparentar el cubo
  a su mano (`NetworkObject.TrySetParent` exige `AllowOwnerToParent` al objeto que se emparenta).
- `NetworkTransform.AuthorityMode: 0 -> 1` (**Owner**): el dueno empuja la transformacion; sin esto sus
  cambios no se replicaban y el servidor los sobrescribia.
- `NetworkTransform.SwitchTransformSpaceWhenParented: 0 -> 1`: al sostenerlo, la transformacion pasa a
  espacio local y el cubo sigue la mano correctamente.

## 4. No habia forma practica de arrancar Host/Client

- **Correccion:** `Lab6RoleManager.OnGUI` muestra un panel con **Start Host (H)**, **Start Client (C)**,
  **Start Server** y **Shutdown (X)**, y el estado (modo, ClientId, rol, botin libre). Funciona en el
  editor, en MPPM y en builds. Ya no depende del Inspector del `NetworkManager`.

## Estado

- Compilacion re-verificada con MSBuild: **0 errores / 0 advertencias**.
- **Pendiente de re-probar** en Unity (salir y volver a entrar en Play; si hace falta, reimportar los
  prefabs `Player` e `ItemGOZU`).
