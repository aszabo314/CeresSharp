
#r "paket: groupref Build //"
#load ".fake/build.fsx/intellisense.fsx"
#load @"paket-files/build/aardvark-platform/aardvark.fake/DefaultSetup.fsx"

open System
open System.IO
open System.Diagnostics
open Aardvark.Fake
open Fake.Core
open Fake.Tools
open Fake.IO.Globbing.Operators
open System.Runtime.InteropServices

do Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
DefaultSetup.install ["src/Ceres.sln"]


entry()
