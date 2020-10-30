﻿using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CADability
{
    public class Parametrics
    {
        private readonly Shell clonedShell;  // the clone of the shell on which to work
        private readonly Dictionary<Face, Face> faceDict; // original to cloned faces
        private readonly Dictionary<Edge, Edge> edgeDict; // original to cloned edges
        private readonly Dictionary<Vertex, Vertex> vertexDict; // original to cloned vertices
        private readonly HashSet<Edge> edgesToRecalculate; // when all surface modifications are done, these edges have to be recalculated
        private readonly HashSet<Vertex> verticesToRecalculate; // these vertices need to be recalculated, because the underlying surfaces changed
        private readonly HashSet<Face> modifiedFaces; // these faces have been (partially) modified, do not modify twice
        private readonly Dictionary<Edge, ICurve> tangentialEdgesModified; // tangential connection between two faces which both have been modified
        public Parametrics(Shell shell)
        {
            faceDict = new Dictionary<Face, Face>();
            edgeDict = new Dictionary<Edge, Edge>();
            vertexDict = new Dictionary<Vertex, Vertex>();
            clonedShell = shell.Clone(edgeDict, vertexDict, faceDict);
            edgesToRecalculate = new HashSet<Edge>();
            modifiedFaces = new HashSet<Face>();
            verticesToRecalculate = new HashSet<Vertex>();
            tangentialEdgesModified = new Dictionary<Edge, ICurve>();
        }
        /// <summary>
        /// the provided face should be moved by the provided offset. All tangentially connected faces are moved with this face.
        /// This is the preparation and can be called multiple times with different faces and different offsets. Finally call <see cref="Result"/> for the modified shell.
        /// </summary>
        /// <param name="toMove"></param>
        /// <param name="offset"></param>
        public void MoveFace(Face toMove, GeoVector offset)
        {
            if (!faceDict.TryGetValue(toMove, out Face faceToMove)) faceToMove = toMove; // toMove may be from the original shell or from the cloned shell
            ModOp move = ModOp.Translate(offset);
            modifiedFaces.Add(faceToMove);
            foreach (Edge edge in faceToMove.AllEdgesIterated())
            {
                Face otherFace = edge.OtherFace(faceToMove);
                bool tangential = edge.IsTangentialEdge();
                if (!modifiedFaces.Contains(otherFace) && tangential)
                {
                    tangentialEdgesModified[edge] = edge.Curve3D.CloneModified(move); // these edges play a role in calculating the new vertices
                    // the edges will be recalculated in "Result()", but here we need the already modified curve for intersection puposes
                    if (!otherFace.Surface.IsExtruded(offset)) MoveFace(otherFace, offset); // only if offset is not the extrusion direction of the otherFace
                } else if (tangential && !tangentialEdgesModified.ContainsKey(edge))
                {
                    tangentialEdgesModified[edge] = edge.Curve3D.CloneModified(move); // these edges play a role in calculating the new vertices
                }
                verticesToRecalculate.Add(edge.Vertex1);
                verticesToRecalculate.Add(edge.Vertex2);
                edgesToRecalculate.UnionWith(edge.Vertex1.AllEdges);
                edgesToRecalculate.UnionWith(edge.Vertex2.AllEdges);
            }
            faceToMove.ModifySurfaceOnly(move); // move after all tangential test have been made, otherwise the tangential tests fail
        }
        /// <summary>
        /// For faces on <see cref="CylindricalSurface"/>, <see cref="ToroidalSurface"/>, (maybe not <see cref="SphericalSurface"/> maybe some swept curve surface)
        /// this face should change its radius resp. <see cref="ToroidalSurface.MinorRadius"/>.
        /// <para>When it is tangential in the direction of the circle to other faces (like a rounded edge is tangential to the faces of the original edge),
        /// then its position will be changed, so that it is still tangential to these faces (i.e. changing the radius of a rounded edge).</para> 
        /// <para>When it is tangential in the other direction (e.g. a cylinder followed by a torus segment, in a pipe or at multiple rounded edges) 
        /// these tangential faces also change their radius and move to the same axis as their predecessor.</para>
        /// </summary>
        /// <param name="toModify">a face from the original shell</param>
        /// <param name="newRadius">the new radius of the surface</param>
        public bool ModifyRadius(Face toModify, double newRadius)
        {
            HashSet<Face> tangential = new HashSet<Face>();
            HashSet<Face> sameSurfaceFaces = new HashSet<Face>();
            HashSet<Edge> sameSurfaceEdges = new HashSet<Edge>();
            if (!faceDict.TryGetValue(toModify, out Face faceToModify)) return false; // must be a face from the original shell
            CollectSameSurfaceFaces(faceToModify, sameSurfaceFaces, sameSurfaceEdges);
            if (sameSurfaceFaces.Count>0)
            {
                // this is probably a splitted full cylinder or torus. we ignore the case that a combination of two parts with the same surface is tangential to other surface
                // in this case the position remains unchanged
                foreach (Face face in sameSurfaceFaces)
                {   // set all faces with identical surfaces the new radius
                    ISurface modifiedSurface = null;
                    if (face.Surface is CylindricalSurface cyl)
                    {
                        modifiedSurface = new CylindricalSurface(cyl.Location, newRadius * cyl.XAxis.Normalized, newRadius * cyl.YAxis.Normalized, cyl.ZAxis);
                    }
                    else if (face.Surface is ToroidalSurface tor)
                    {
                        modifiedSurface = new ToroidalSurface(tor.Location, tor.XAxis.Normalized, tor.YAxis.Normalized, tor.ZAxis.Normalized, tor.XAxis.Length, newRadius);
                    }
                    else if (face.Surface is SphericalSurface sph)
                    {
                        modifiedSurface = new SphericalSurface(sph.Location, newRadius * sph.XAxis.Normalized, newRadius * sph.YAxis.Normalized, newRadius * sph.ZAxis.Normalized);
                    }
                    face.Surface = modifiedSurface; 
                    verticesToRecalculate.UnionWith(face.Vertices);
                }
                foreach (Vertex vtx in verticesToRecalculate)
                {
                    edgesToRecalculate.UnionWith(vtx.AllEdges);
                }
                foreach (Edge edge in sameSurfaceEdges)
                {   // the new tangential edges are easy to calculate here
                    if (edge.Forward(edge.PrimaryFace)) tangentialEdgesModified[edge] = edge.PrimaryFace.Surface.Make3dCurve(edge.PrimaryCurve2D);
                    else tangentialEdgesModified[edge] = edge.SecondaryFace.Surface.Make3dCurve(edge.SecondaryCurve2D);
                }
                return true;
            }
            else
            {   // there are no other faces with the same surface, so we have to check, whether this face is tangential to some other faces
                foreach (Edge edge in faceToModify.AllEdgesIterated())
                {
                    Face otherFace = edge.OtherFace(faceToModify);
                    if (edge.IsTangentialEdge())
                    {
                        tangential.Add(otherFace);
                    }
                }
                if (tangential.Count==2)
                {
                    // ok, here is a lot to do: cylinder get new axis, torus gets new major radius
                }
            }
            return false;
        }
        /// <summary>
        /// Find all faces that share a common surface
        /// </summary>
        /// <param name="face">start with this face</param>
        /// <param name="sameSurfaceFaces">the faces found</param>
        /// <param name="sameSurfaceEdges">the edges, that connect these faces</param>
        private void CollectSameSurfaceFaces(Face face, HashSet<Face> sameSurfaceFaces, HashSet<Edge> sameSurfaceEdges)
        {
            if (sameSurfaceFaces.Contains(face)) return; // already tested
            sameSurfaceFaces.Add(face);
            foreach (Edge edge in face.AllEdgesIterated())
            {
                Face otherFace = edge.OtherFace(face);
                if (edge.IsTangentialEdge())
                {
                    if (otherFace.Surface.SameGeometry(otherFace.Domain, face.Surface, face.Domain, Precision.eps, out _))
                    {
                        sameSurfaceEdges.Add(edge);
                        CollectSameSurfaceFaces(otherFace, sameSurfaceFaces,sameSurfaceEdges);
                    }
                }
            }
        }

        public Shell Result(out HashSet<Face> involvedFaces)
        {
            involvedFaces = new HashSet<Face>();
            foreach (Vertex vertex in verticesToRecalculate)
            {
                bool done = false;
                // first lets see, whether two tangential surfaces are involved with this vertex. Then we need to intersect the tangential edge with the third surface
                foreach (Edge edge in vertex.AllEdges) // there are at least three
                {
                    ICurve crv; // either a already moved tangential edge or an unmodified edge
                    if (!tangentialEdgesModified.TryGetValue(edge, out crv) && edge.IsTangentialEdge()) crv = edge.Curve3D;
                    if (crv != null)
                    {   // this vertex is the start or endvertex of a tangential edge. We cannot use the intersection of 3 surfaces to calculate its new position
                        foreach (Face face in vertex.InvolvedFaces)
                        {
                            if (face != edge.PrimaryFace && face != edge.SecondaryFace)
                            {   // this is the face (or very rare one of the faces) that is not part of the tangential edges
                                face.Surface.Intersect(crv, face.Domain, out GeoPoint[] ips, out GeoPoint2D[] uvOnSurface, out double[] uOnCurve); // the domain should only be used for periodic adjustment!
                                if (ips.Length > 0) // there should always be such an intersection point, otherwise there is no way to modify the shell
                                {
                                    if (ips.Length > 1)
                                    {
                                        // find best point: closer to startpoint or endpoint of crv, depending on vertex, set it on ips[0]
                                        GeoPoint testPoint = GeoPoint.Invalid;
                                        if (edge.Vertex1 == vertex) testPoint = crv.StartPoint;
                                        else if (edge.Vertex2 == vertex) testPoint = crv.EndPoint;
                                        if (testPoint.IsValid)
                                        {
                                            double mindist = double.MaxValue;
                                            for (int i = 0; i < ips.Length; i++)
                                            {
                                                double dist = ips[i] | testPoint;
                                                if (dist<mindist)
                                                {
                                                    mindist = dist;
                                                    ips[0] = ips[i];
                                                }
                                            }
                                        }
                                    }
                                    vertex.Position = ips[0];
                                    done = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (done) break;
                }
                Face[] faces = vertex.InvolvedFaces.ToArray();
                if (!done && faces.Length >= 3)
                {   // here we would need to be more selective
                    GeoPoint ip = vertex.Position;
                    // maybe two faces have an identical surface (splitted periodic surface)
                    double mindist = double.MaxValue;
                    foreach (Edge edg in vertex.AllEdges)
                    {
                        if (edg.PrimaryFace.Surface.SameGeometry(edg.PrimaryFace.Domain, edg.SecondaryFace.Surface, edg.SecondaryFace.Domain, Precision.eps, out ModOp2D dumy))
                        {
                            HashSet<Face> vertexFaces = new HashSet<Face>(vertex.InvolvedFaces);
                            vertexFaces.Remove(edg.PrimaryFace);
                            vertexFaces.Remove(edg.SecondaryFace);
                            if (vertexFaces.Count > 0)
                            {
                                Face fc = vertexFaces.First();
                                fc.Surface.Intersect(edg.Curve3D, fc.Domain, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve);
                                for (int i = 0; i < ips.Length; i++)
                                {
                                    double d = ips[i] | vertex.Position;
                                    if (d < mindist)
                                    {
                                        mindist = d;
                                        ip = ips[i];
                                    }
                                }
                            }
                        }
                    }
                    if (mindist != double.MaxValue)
                    {
                        vertex.Position = ip;
                        done = true;
                    }
                    if (!done)
                    {   
                        if (Surfaces.NewtonIntersect(faces[0].Surface, faces[0].Domain, faces[1].Surface, faces[1].Domain,
                            faces[2].Surface, faces[2].Domain, ref ip, out GeoPoint2D uv0, out GeoPoint2D uv1, out GeoPoint2D uv2))
                        {
                            vertex.Position = ip;
                            done = true;
                        }
                    }
                }
                if (!done) return null; // position of vertex could not be calculated
            }
            foreach (Edge edge in edgesToRecalculate)
            {
                List<GeoPoint> seeds = new List<GeoPoint>(); // for the intersection
                seeds.Add(edge.Vertex1.Position); // the vertices have already their new positions, the edges must start and end here
                seeds.Add(edge.Vertex2.Position);
                ICurve crv = null;
                if (edge.PrimaryFace.Surface.SameGeometry(edge.PrimaryFace.Domain, edge.SecondaryFace.Surface, edge.SecondaryFace.Domain, Precision.eps, out ModOp2D firstToSecond))
                {   // this is probably a seam of two periodic parts with the same surface
                    GeoPoint2D sp = edge.PrimaryFace.Surface.PositionOf(seeds[0]);
                    GeoPoint2D ep = edge.PrimaryFace.Surface.PositionOf(seeds[1]);
                    SurfaceHelper.AdjustPeriodic(edge.PrimaryFace.Surface, edge.PrimaryFace.Domain, ref sp);
                    SurfaceHelper.AdjustPeriodic(edge.PrimaryFace.Surface, edge.PrimaryFace.Domain, ref ep);
                    Line2D l2d = new Line2D(sp, ep);
                    crv = edge.PrimaryFace.Surface.Make3dCurve(l2d);
                }
                if (crv == null) crv = Surfaces.Intersect(edge.PrimaryFace.Surface, edge.PrimaryFace.Domain, edge.SecondaryFace.Surface, edge.SecondaryFace.Domain, seeds);
                if (crv != null)
                {   // there should not be closed curves as edges
                    if ((crv.StartPoint | seeds[0]) > (crv.StartPoint | seeds[1])) crv.Reverse();
                    if (crv is InterpolatedDualSurfaceCurve)
                    { // what to do here?
                    }
                    else
                    {
                        crv.StartPoint = seeds[0];
                        crv.EndPoint = seeds[1];
                    }
                    edge.Curve3D = crv;
                    edge.PrimaryCurve2D = edge.PrimaryFace.Surface.GetProjectedCurve(crv, 0.0);
                    if (!edge.Forward(edge.PrimaryFace)) edge.PrimaryCurve2D.Reverse();
                    SurfaceHelper.AdjustPeriodic(edge.PrimaryFace.Surface, edge.PrimaryFace.Domain, edge.PrimaryCurve2D);
                    edge.SecondaryCurve2D = edge.SecondaryFace.Surface.GetProjectedCurve(crv, 0.0);
                    if (!edge.Forward(edge.SecondaryFace)) edge.SecondaryCurve2D.Reverse();
                    SurfaceHelper.AdjustPeriodic(edge.SecondaryFace.Surface, edge.SecondaryFace.Domain, edge.SecondaryCurve2D);
                    edge.Orient();
                    involvedFaces.Add(edge.PrimaryFace);
                    involvedFaces.Add(edge.SecondaryFace);
                }
                else return null; // edge could not be recalculated, the modification is not possible
            }
            foreach (Face face in involvedFaces)
            {
                face.ForceAreaRecalc();
            }
            if (clonedShell.CheckConsistency()) return clonedShell;
            return null;
        }
    }
}
