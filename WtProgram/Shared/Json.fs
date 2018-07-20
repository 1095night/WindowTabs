﻿namespace Bemo
open System
open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Linq

[<AutoOpen>]
module JObjectHelper =
    type System.Collections.Generic.IDictionary<'k,'v> with
        member this.GetValue(key) =
            if this.ContainsKey(key) then Some(this.Item(key)) else None

    type JObject with 
        member this.items = List2(this:>IDictionary<_,_>).map(fun pair -> pair.Key,pair.Value)
        member this.getString(key) = this.GetValue(key).map(fun t -> unbox<string>((t :?> JValue).Value))
        member this.getBool(key) = this.GetValue(key).map(fun t -> unbox<bool>((t :?> JValue).Value))
        member this.getInt32(key) = this.GetValue(key).map(fun t -> unbox<int64>((t :?> JValue).Value).Int32)
        member this.getIntPtr(key) = this.GetValue(key).map(fun t -> IntPtr(unbox<int64>((t :?> JValue).Value)))
        member this.getPt(key) = Pt(this.getInt32("x").Value, this.getInt32("y").Value)
        member this.getSz(key) = Sz(this.getInt32("width").Value, this.getInt32("height").Value)
        member this.getRect(key) = Rect(this.getPt("location"), this.getSz("size"))
        member this.getArray<'t>(key) =
            let parse(token:JToken) =
                List2(token :?> JArray).map(fun t -> unbox<'t>((t :?> JValue).Value))
            this.GetValue(key).map(parse)
        member this.getObjectArray(key) = 
            let parse(token:JToken) =
                List2(token :?> JArray).map(fun t -> t :?> JObject)
            this.GetValue(key).map(parse)
        member this.getStringArray(key) = this.getArray<string>(key)
        member this.getInt32Array(key) = this.getArray<Int64>(key).map(fun l -> l.map(int32))
        member this.getObject(key) = this.GetValue(key).map(fun t -> unbox<JObject>(t))
        member this.update(key:string, token:JToken option) =
            match token with
            | Some(token) ->
                if (this :> IDictionary<_,_>).ContainsKey(key) then this.Remove(key).ignore
                this.Add(key, token)
            | None -> this.Remove(key).ignore
        member this.addOrUpdate(key:string, token:JToken) =
            this.update(key, Some(token))

        member this.setValue(key, value:obj) =
            this.addOrUpdate(key, JValue(value))
        member this.setValueArray(key, values:List2<_>) =
            this.addOrUpdate(key, JArray(values.map(fun value -> box(JValue(box(value)))).toArray))
        member this.setObject(key, value:JObject) =
            this.addOrUpdate(key, value)
        member this.setObjectArray(key, values:List2<JObject>) =
            this.addOrUpdate(key, JArray(values.map(fun value -> box(value)).toArray))

        member this.setStringArray(key, value:List2<string>) = 
            this.setValueArray(key, value)
        member this.setInt32Array(key, value:List2<Int32>) = 
            this.setValueArray(key, value)
        member this.setBool(key, value:bool) =
            this.setValue(key, value)
        member this.setInt32(key, value:int32) =
            this.setValue(key, value)
        member this.setInt64(key, value:int64) =
            this.setValue(key, value)
        member this.setIntPtr(key, value:IntPtr) =
            this.setValue(key, value.ToInt64())
        member this.setString(key, value:string) =
            this.setValue(key, value)
        member this.setPt(key, value:Pt) =
            this.setInt32("x", value.x)
            this.setInt32("y", value.y)
        member this.setSz(key, value:Sz) =
            this.setInt32("width", value.width)
            this.setInt32("height", value.height)
        member this.setRect(key, value:Rect) =
            this.setPt("location", value.location)
            this.setSz("size", value.size)
            