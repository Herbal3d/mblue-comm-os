# Architecture

This project is the OpenSimulator interface that takes world and world
contents and interfaces them to the [mblue.ecm](https://github.com/Herbal3d/mblue-ecm/)
system. That is, the contents of the viewed part of the an OpenSimulator grid
is received from the servers and echoed in the ECM in-memory storage system.
Those representations and then displayed by the graphics system that is also
connected to the ECM system.

# OpenSimulator Contents in the ECM

As information about the OpenSimulator world are received, entries in
ECM are created. The IEntity is created and Components are added depending
on the type of entity.

Components added to entities:

| COMPONENT       | CONTAINS AND WHEN ADDED |
| :---:           | :---: |
| CmptOSInfo      | pointers back to the LibreMetaverse structures. `IGridClient` in particular. |
| LLCmptLocation  | wrapper for ICmptLocation which links into the LibreMetaverse data structures. |
| LLCmptAnimation | added if the entity has an animation attached to it. |
| LLCmptDisplayable | wrapper for ICmptDisplayable that contains the converted display information. |
| LLCmptAvatar    | added if an avatar. |

