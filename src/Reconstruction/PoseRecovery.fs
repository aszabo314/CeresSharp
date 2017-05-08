﻿namespace Aardvark.Reconstruction

open System
open Aardvark.Base
open Aardvark.Base.MiniCV

module Estimate = 
    open Aardvark.Base.MultimethodTest

    let LtoRTrafo (cfg : RecoverPoseConfig) (l : V2d[]) (r : V2d[]) =
            
        let (i,R,t,mask) = MiniCV.recoverPose cfg l r

        printfn "inliers: %A (of %A)" (mask |> Array.sumBy int) (l |> Array.length)

        mask, Trafo3d.FromBasis(R.C0, R.C1, R.C2, t)
   
    

    let camsFromMatches (cfg : RecoverPoseConfig) (cameraTree : RoseTree<int>) (getMatches : int -> int -> MapExt<TrackId, (V2d * V2d)> ) =
        
        let register ci parent =
            let ms =  getMatches parent ci
            let (a, b) = ms |> MapExt.toArray |> Array.map snd |> Array.unzip
            let idx = ms |> MapExt.toArray |> Array.map fst
            let (mask, trafo) = LtoRTrafo cfg a b
            let inliers = 
                [
                    for i in 0..mask.Length-1 do
                        yield idx.[i], (CameraId(ci),CameraId(parent),(mask.[i] > 0uy))
                ] |> MapExt.ofList

            inliers,trafo


        let rec traverse (cur : Trafo3d) (inliers : MapExt<TrackId, MapExt<CameraId * CameraId, bool>>) (parent : int) (remaining : RoseTree<_>) =
            
            let mkInliers inl =
                let mutable super = inliers
                for KeyValue(tid, (cidSelf,cidParent,isInlier)) in inl do
                    let newMap = [ (cidSelf,cidParent),isInlier ] |> MapExt.ofList
                    super <- super |> MapExt.alter tid ( fun map ->
                                        match map with
                                        | None -> Some newMap
                                        | Some m -> Some (newMap |> MapExt.union m)
                                        )
                super
                
            match remaining with
            | Empty -> MapExt.empty,Empty
            | Leaf ci ->
                let (inl, trafo) = register ci parent
                let trafo = cur * trafo
                let inliers = mkInliers inl
                inliers, Leaf(ci, trafo)
            | Node (ci, children) ->
                let (inl, trafo) = register ci parent
                let inliers = mkInliers inl
                let trafo = cur * trafo
                let (inliers, children) = 
                    let oo = children |> List.map (traverse trafo inliers ci)
                    (oo |> List.map fst 
                        |> List.fold (MapExt.unionWith (fun l r -> l |> MapExt.unionWith (fun a b -> Log.warn "&& occurred ..... this is bad "; a && b) r)) MapExt.empty), //this && should never occur 
                     oo |> List.map snd
                inliers, Node((ci, trafo), children )

        let (inliers, trafoTree) = 
            match cameraTree with
            | Empty -> MapExt.empty, Empty
            | Leaf i ->
                MapExt.empty, Leaf(i, Trafo3d.Identity)
            | Node(i, children) ->
                let trafo = Trafo3d.Identity
                let (inliers, children) = 
                    let oo = children |> List.map (traverse trafo MapExt.empty i)
                    (oo |> List.map fst 
                        |> List.fold (MapExt.unionWith (fun l r -> l |> MapExt.unionWith (&&) r)) MapExt.empty), 
                     oo |> List.map snd
                inliers, Node((i, trafo), children)
            
        inliers,
        trafoTree 
            |> RoseTree.toList
            |> List.map ( fun (ci,trafo) -> 
                 let oc = Camera3d.LookAt(V3d.IOO * 10.0, V3d.Zero, 1.0, V3d.OOI)
                 let forward = AngleAxis.RotatePoint(-oc.AngleAxis, -V3d.OOI)
                 let trafo = (Trafo3d.Rotation(forward, -Math.PI/2.0)) * trafo
                 ci, oc.Transformed trafo
                )
         
    
    let pointsFromCams (tracks : MapExt<TrackId, MapExt<CameraId, V2d>>) ( getRay : CameraId -> V2d -> Ray3d ) =
        tracks 
            |> MapExt.map
                ( fun tid track ->
                    track   |> MapExt.toList
                            |> List.map ( fun (ci,obs) -> getRay ci obs)
                            |> Ray3d.avg
                )
