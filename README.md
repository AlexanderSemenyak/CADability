# CADability

_**(Please allow a few days for getting everything in its right place. The project has been published as open source on 3 November 2020)**_

CADability is a .NET class library and a .NET application that implements a 3d CAD system. You can use this library with or without user interface.

Some of the features you might be interested in are:
- Analyze all data (geometrical entities, attributes) of the CAD model. 
- Data exchange with many CAD databases or file formats (STEP, DWG, DXF, STL)
- Extensible data model and user interface.
- Simple Windows.Forms.Control to display the CAD model and integrate it in your application.
- User interface to interact with the geometric data, select objects, show and modify their properties, do 3d modeling.

The CADability solution is composed of two class libraries and an application:
- CADability, a dll, which contains all the classes of the geometrical objects, the action classes, the geometrical calculations and algorithms (e.g. 3d modeling), 
the structure (but not the graphical implementation) of the user interface and some organizational classes.
- CADability.Forms, a dll, which contains the graphical implementation of the user interface based on Windows.Forms and the connection to the windows platform.
- CADability.App, an exe, which is a very thin container of CADability.Forms

For your first experience you can simply build and start the solution and try to draw or construct 3d objects. Typically you will replace the CADability.App 
by your own application or use only the CADability.dll to analyze or compose 3d models.

Here is a quick overview of the [organizational classes](articles/orgclass.html).

This is an overview of the CAD [database](articles/database.html), the geometrical entities that make up a model.

And this is the [complete table of contents](api/toc.html).
