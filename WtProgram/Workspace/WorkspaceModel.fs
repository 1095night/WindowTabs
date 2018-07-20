namespace Bemo
open System
open System.Collections.Generic
open System.Drawing
open System.IO
open System.Text.RegularExpressions
open System.Windows.Forms
open Bemo.Win32.Forms
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Aga.Controls
open Aga.Controls.Tree

type IEditInfo =
    abstract member title : string
    abstract member fields : List2<string * Control>
    abstract member height : int
    abstract member ok : unit -> unit

[<AllowNullLiteral>]
type IWorkspaceNode =
    inherit INode
    abstract member beginEdit : unit -> IEditInfo
    abstract member remove : unit -> unit
    abstract member removed: IEvent<unit>

type WorkspaceWindowTitleMatchType =
    | ExactMatch = 0
    | StartsWith = 1
    | EndsWith = 2
    | Contains = 3
    | RegEx = 4

type WorkspaceWindow() as this = 
    inherit Dynamic()
    let _icon = Services.openImage("window.png")
    let removedEvent = Event<_>()
    let data = ModelObject()

    member this.name 
        with get() = data.get("name").cast<string>()
        and set(value) = data.set("name", value)

    member this.title 
        with get() = data.get("title").cast<string>()
        and set(value) = data.set("title", value)

    member this.matchType 
        with get() = data.get("matchType").cast<WorkspaceWindowTitleMatchType>()
        and set(value) = data.set("matchType", value)

    member this.zorder 
        with get() = data.get("zorder").cast<int>()
        and set(value) = data.set("zorder", value)

    member this.icon = _icon
    member this.children = List2<Dynamic>()
    interface IWorkspaceNode with
        member x.showSettings = true
        member x.remove() =
            removedEvent.Trigger()
        member x.removed = removedEvent.Publish
        member x.beginEdit() =
            let nameEditor = TextEditor() :> IPropEditor
            nameEditor.value <- this.name
            let titleEditor = TextEditor() :> IPropEditor
            titleEditor.value <- this.title
            let matchTypeEditor = EnumEditor<WorkspaceWindowTitleMatchType>()
            matchTypeEditor.value <- this.matchType
            { new IEditInfo with
                member x.title = this.name
                member x.fields = 
                    List2([
                        ("Name", nameEditor.control)
                        ("Title", titleEditor.control)
                        ("Match Type", matchTypeEditor.cast<IPropEditor>().control)
                    ])
                member x.height  = 250
                member x.ok() = 
                    this.name <- nameEditor.value.cast<string>()
                    this.title <- titleEditor.value.cast<string>()
                    this.matchType <- matchTypeEditor.value
            }

    member this.serialize() =
        let obj = JObject()
        obj.setString("name", this.name)
        obj.setString("title", this.title)
        obj.setInt32("zorder", this.zorder)
        obj.setInt32("matchType", int32(this.matchType))
        obj

    static member deserialize(obj:JObject) =
        let window = WorkspaceWindow()
        window.name <- obj.getString("name").Value
        window.title <-  obj.getString("title").Value
        window.zorder <- obj.getInt32("zorder").Value
        window.matchType <- enum<WorkspaceWindowTitleMatchType>(obj.getInt32("matchType").Value)
        window
    
    
and 
    [<AllowNullLiteral>]
    WorkspaceGroup() as this =
    inherit Dynamic()
    [<DefaultValue>] val mutable name : string
    [<DefaultValue>] val mutable placement : OSWindowPlacement
    [<DefaultValue>] val mutable workspace : Workspace
    let mutable _windows  = System.Collections.Generic.List<Dynamic>()
    let removedEvent = Event<_>()

    member this.addWindow(window) =
        _windows.Add(window)
        window.cast<IWorkspaceNode>().removed.Add <| fun()-> this.removeWindow(window)

    member this.removeWindow(window) =
        _windows.Remove(window).ignore

    member this.windows = List2(_windows)
    member this.children = this.windows
    
    interface IWorkspaceNode with
        member x.showSettings = false
        member x.remove() =
            removedEvent.Trigger()
        member x.removed = removedEvent.Publish
        member x.beginEdit() =
            let nameEditor = TextEditor() :> IPropEditor
            nameEditor.value <- this?name
            { new IEditInfo with
                member x.title = this?name
                member x.fields = List2([("Name", nameEditor.control)])
                member x.height  = 200
                member x.ok() = this?name <- nameEditor.value.cast<string>()
            }
    
    member this.serialize() =
        let placementObj = 
            let obj = JObject()
            obj.setInt32("showCmd", this.placement.showCmd)
            obj.setPt("ptMaxPosition", this.placement.ptMaxPosition)
            obj.setPt("ptMinPosition", this.placement.ptMinPosition)
            obj.setRect("rcNormalPosition", this.placement.rcNormalPosition)
            obj
        let windowObjects = this.children.map <| fun child -> child?serialize()
        let groupObj = JObject()
        groupObj.setString("name", this.name)
        groupObj.setObject("placement", placementObj)
        groupObj.setObjectArray("windows", windowObjects)
        groupObj

    static member deserialize(obj:JObject) =
        let group = WorkspaceGroup(
            name = obj.getString("name").Value,
            placement =(
                let obj = obj.getObject("placement").Value
                {
                    flags = 0
                    showCmd = obj.getInt32("showCmd").Value
                    ptMaxPosition = obj.getPt("ptMaxPosition")
                    ptMinPosition = obj.getPt("ptMinPosition")
                    rcNormalPosition = obj.getRect("rcNormalPosition")
                })
        )
        obj.getObjectArray("windows").Value.map(WorkspaceWindow.deserialize).iter(group.addWindow)
        group


and 
    [<AllowNullLiteral>]
    Workspace() as this =
    inherit Dynamic()
    let removedEvent = Event<_>()
    [<DefaultValue>] val mutable name : string
    let mutable _groups  = System.Collections.Generic.List<Dynamic>()
    let _icon = Services.openImage("workspace.png")
    
    member this.addGroup(group) =
        group.cast<IWorkspaceNode>().removed.Add <| fun()-> this.removeGroup(group)
        _groups.Add(group)

    member this.removeGroup(group) =
        _groups.Remove(group).ignore

    member this.groups = List2(_groups)
    member this.children = this.groups
    member this.icon = _icon

    interface IWorkspaceNode with
        member x.showSettings = false
        member x.remove() =
            removedEvent.Trigger()
        member x.removed = removedEvent.Publish
        member x.beginEdit() =
            let nameEditor = TextEditor() :> IPropEditor
            nameEditor.value <- this?name
            { new IEditInfo with
                member x.title = this?name
                member x.fields = List2([("Name", nameEditor.control)])
                member x.height  = 200
                member x.ok() = this?name <- nameEditor.value.cast<string>()
            }

    member this.serialize() =
        let layoutObj = JObject()
        layoutObj.setString("name", this.name)
        layoutObj.setObjectArray("groups", this.children.map <| fun child-> child?serialize())
        layoutObj

    static member deserialize(obj:JObject) =
        let groups = obj.getObjectArray("groups").Value.map(WorkspaceGroup.deserialize)
        let ws = Workspace(
            name = obj.getString("name").Value
        )
        groups.iter(ws.addGroup)
        ws

type WindowResolver() as this =
    let os = OS()
    let mutable hwnds = Services.program.appWindows
    let hwndToTitle = Map2(hwnds.map(fun hwnd -> (hwnd, os.windowFromHwnd(hwnd).text)))

    member this.title(hwnd) = hwndToTitle.find(hwnd)
    member this.removeHwnd(hwnd) =
        hwnds <- hwnds.where((<>) hwnd)

    member this.resolve(windowInfo:Dynamic) =
        let target : string = windowInfo?title

        let isMatch =
            match windowInfo?matchType with
            | WorkspaceWindowTitleMatchType.ExactMatch ->
                fun(title) -> title = target
            | WorkspaceWindowTitleMatchType.Contains ->
                fun(title) -> title.Contains(target)
            | WorkspaceWindowTitleMatchType.StartsWith ->
                fun(title) -> title.StartsWith(target)
            | WorkspaceWindowTitleMatchType.EndsWith ->
                fun(title) -> title.EndsWith(target)
            | WorkspaceWindowTitleMatchType.RegEx ->
                let re = Regex(target)
                fun(title) -> re.IsMatch(title)
            | _ -> fun(title) -> false

        hwnds.tryFind(this.title >> isMatch)
  

type IWorkspaceModel =
    abstract member list : List2<Workspace>
    abstract member create : string -> unit
    abstract member restore : int
    abstract member update : int * Workspace -> unit
    abstract member delete : int -> unit
      
type WorkspaceModel() as this =
    inherit Dynamic()
    let os = OS()
    let workspaceAddedEvt = Event<_>()
    let selectedChangedEvt = Event<_>()
    let canRestoreChangedEvt = Event<_>()
    let _workspaces = System.Collections.Generic.List<Workspace>()
    let mutable _selected = null : obj

    do
        Observable.init(this)

    member this.workspaces =  _workspaces.list
    member this.workspaceAdded = workspaceAddedEvt.Publish

    member this.selected
        with get() = _selected
        and set(value) =
            _selected <- value
            selectedChangedEvt.Trigger(value)
            canRestoreChangedEvt.Trigger(this.canRestore)

    member this.selectedChanged = selectedChangedEvt.Publish

    member private this.newWorkspaceName() =
        let nextNumber = this.workspaces.choose(fun(w) -> w.name.Replace("Workspace ", "").tryToInt()).maxBy 0 id + 1
        sprintf "Workspace %A" nextNumber

    member private this.createWorkspace() =
        let zorder = os.windowZorders
        let groups = Services.desktop.groups.enumerate.map <| fun (i, group) ->
            let windowsInZorder = group.windows.sortBy(zorder.find)
            let innerZorder = Map2(windowsInZorder.enumerate.map(fun(innerZorder, hwnd) -> hwnd, innerZorder))
            let wsGroup = WorkspaceGroup(
                name = sprintf "Group %d" (i + 1),
                placement = (
                    let hwnd = windowsInZorder.head
                    os.windowFromHwnd(hwnd).placement)
            )
            group.windows.enumerate.iter <| fun (j, hwnd) ->
                let window = os.windowFromHwnd(hwnd)
                let ww = WorkspaceWindow()
                ww.name <- window.pid.exeName
                ww.title <- window.text
                ww.zorder <- innerZorder.find(hwnd)
                ww.matchType <- WorkspaceWindowTitleMatchType.ExactMatch
                wsGroup.addWindow(ww)
            wsGroup
            
        let ws = Workspace(
            name = this.newWorkspaceName()
        )

        groups.iter(ws.addGroup)
        ws

    member private this.restoreWorkspace(workspace:Workspace) =
        let windowResolver = WindowResolver()
      
        Services.program.suspendTabMonitoring()

        let hwndToGroup = Map2(Services.desktop.groups.collect <| fun group ->
            group.windows.map <| fun hwnd -> (hwnd, group)
        )
        let removeWindow hwnd = hwndToGroup.find(hwnd).removeWindow(hwnd)

        workspace.children.iter <| fun (groupInfo) ->
            let windows : List2<Dynamic> = groupInfo?windows
            let windows = windows.reverse
            let windows = windows.sortBy(fun w -> w?zorder).choose windowResolver.resolve
            
            windows.iter removeWindow
            windows.iter <| fun hwnd -> WinUserApi.ShowWindow(hwnd, ShowWindowCommands.SW_RESTORE).ignore
            windows.iter <| fun hwnd -> os.windowFromHwnd(hwnd).setPlacement(groupInfo?placement)
            os.setZorder(windows)

            let group = Services.desktop.createGroup(Services.settings.getValue("combineIconsInTaskbar").cast<bool>())
            windows.iter <| fun hwnd -> group.addWindow(hwnd, false)

        Services.program.resumeTabMonitoring()

    member this.addWorkspace(ws:Workspace) =
        ws.cast<IWorkspaceNode>().removed.Add <| fun() -> this.onWorkspaceRemoved(ws)
        _workspaces.Add(ws)
        this.saveSettings() 
        workspaceAddedEvt.Trigger(ws) 
         
    member this.create() =
        let ws = this.createWorkspace()
        this.addWorkspace(ws)
    
    member this.remove() =
        if this.selected <> null then
            this.selected?remove()

    member this.canRestore =
        this.selected <> null && this.selected.GetType() = typeof<Workspace>

    member this.canRestoreChanged = canRestoreChangedEvt.Publish

    member this.restore() =
        if this.selected <> null then
            let ws = this.selected :?> Workspace
            this.restoreWorkspace(ws)

    member this.edit(parent) =
        let selected = this.selected
        if selected <> null then
            let editInfo = selected?beginEdit()
            let table = UIHelper.form(editInfo?fields)
            let form = UIHelper.okCancelForm table
            let icon = Services.openIcon("edit.ico")
            form.Icon <- icon
            form.Width <- 300
            form.Height <- editInfo?height
            form.StartPosition <- FormStartPosition.CenterParent
            form.Text <- editInfo?title
            let ok = form.ShowDialog(parent) = DialogResult.OK
            if ok then    
                editInfo?ok()
                this.saveSettings()
            ok
        else
            false

    member this.init() =
        this.loadSettings()

    member this.loadSettings() =
        let settingsObj = Services.settings.root
        let workspaces = settingsObj.getObjectArray("workspaces").def(List2()).map(Workspace.deserialize)
        workspaces.iter this.addWorkspace

    member this.saveSettings() =
        let settingsObj = Services.settings.root
        let workspaceObjs = this.workspaces.map <| fun ws -> ws.serialize()
        settingsObj.setObjectArray("workspaces", workspaceObjs)
        Services.settings.root <- settingsObj


    member this.onWorkspaceRemoved(ws) =
        _workspaces.Remove(ws).ignore
        this.saveSettings() 