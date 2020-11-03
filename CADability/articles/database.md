# Introduction to CADability's data model

Geometric entities are the main objects in the CADability database.

Usually many geometric entities are contained in a [Model](../api/CADability.Model.html). If a model is displayed in a [ModelView](../api/CADability.ModelView.html) the geometric entities 
are displayed on the screen (in a [CadCanvas](../../CADabilityFormsDoc/api/CADability.Forms.CadCanvas.html) inside a [CadFrame](../../CADabilityFormsDoc/api/CADability.Forms.CadFrame.html)). 

The entity that is normally saved in a file is the [Project](../api/CADability.Project.html). The project contains one or more models and some other information, like lists of attributes.

## What are geometric entities?
There are some families of geometric entities:
- Curves (e.g. [Line](../api/CADability.GeoObject.Line.html), [BSpline](../api/CADability.GeoObject.BSpline.html), [Ellipse](../api/CADability.GeoObject.Ellipse.html) etc., in a 3d coordinate space)
- [Block](../api/CADability.GeoObject.Block.html)s (collections of geometric entities, hierarchical tree of objects)
- BRep objects (e.g. [Solid](../api/CADability.GeoObject.Solid.html), [Face](../api/CADability.GeoObject.Face.html), etc.) (see [Wikipedia](https://en.wikipedia.org/wiki/Boundary_representation))
- Text, Dimension, Hatches

Geometric entities can be created and manipulated both interactively and by program code.

Geometric entities are serializable as JSON files. A [Model](../api/CADability.Model.html) can be exported to or imported from a STEP file.

## What do geometric entities have in common?
From a programmer's point of view it is the [IGeoObject](../api/CADability.GeoObject.IGeoObject.html) interface.

Geometric entities may be
- visualized (in a [Model](../api/CADability.Model.html) which is contained in a [ModelView](../api/CADability.ModelView.html))
- serialized in a JSON file
- selected with mouse-clicks in various ways
- modified by a [ModOp](../api/CADability.ModOp.html) (i.e. moved, rotated scaled, reflected etc.)
- displayed in a property grid, where their properties are editable
- interactively created or modified by various [Action](../api/CADability.Actions.Action.html)s.

## How do I connect additional information to geometric entities?
Use the [UserData](../api/CADability.UserData.html) object and the IGeoObject.[UserData](../api/CADability.GeoObject.IGeoObject.html#CADability_GeoObject_IGeoObject_UserData) property. This property acts like a dictionary that lets you add any data to the geometric entity. 
If your data is serializable it will also be saved in the CADability database. If your data implements the IPropertyEntry interface or is a simple data type like int, double or string, it will 
be displayed in the control center (when the object is selected).

## How do I define my own geometric entity?
You almost never need to define or code your own geometric entities. But still, if you want to, there are several ways to define customized geometric entities:
- Define a class that implements the [IGeoObject](../api/CADability.GeoObject.IGeoObject.html) and [IJsonSerialize](../api/CADability.IJsonSerialize.html) interfaces. 
This is certainly a huge task and in most cases not necessary.
- Derive a class from [IGeoObjectImpl](../api/CADability.GeoObject.IGeoObjectImpl.html). This is a standard implementation of IGeoObject but still leaves a lot of work to do.
- Derive a class from [Block](../api/CADability.GeoObject.Block.html). Add some additional properties and create child entities according to these properties. 
Maybe override [GetShowProperties](../api/CADability.GeoObject.IGeoObject.GetShowProperties.html) to define it's appearance in the control center or property grid.

