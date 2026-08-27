# Architecture

This project is the OpenSimulator interface that takes world and world
contents and interfaces them to the [mblue.ecm](https://github.com/Herbal3d/mblue-ecm/)
system. That is, the contents of the viewed part of the an OpenSimulator grid
is received from the servers and echoed in the ECM in-memory storage system.
Those representations and then displayed by the graphics system that is also
connected to the ECM system.

# OpenSimulator Contents in the ECM

The connection to the OpenSimulator grid is represented by an ECM `IEntity`
that has a `LLCmptWorld` component. The `LLCmptWorld` component is the handle
to the user's identity and authentication and keeps the connections to the
simulator and has handles to the various interface to communicating with
the simulator. 

`LLCmptWorld` contains:

- user login parameters
- user login response (the block of parameters returned by the OpenSimulator server)
- list of regions

The `ICommProvider` for the OpenSimulator connection (`CommLLLP`) uses `LLCmptWorld`
as the storage for structures relating to the connection. As listed above,
that includes the information about the login session and the regions that
are being tracked.

As information about entities are received by by `CommLLLP`, entities are created
and added to `mblue.ecm`. These entities will contain components that point
back to the structures built by `LibreMetaverse` and, except what is required
for graphics display (mostly the built `IDisplayable`), this tries to not create
new infrastructure between the communication system and the eventual display.