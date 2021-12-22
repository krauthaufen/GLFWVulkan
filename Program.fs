open FSharp.Data.Adaptive
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Application
open Aardvark.SceneGraph
open Silk.NET.GLFW
open Glfw

module AdaptiveFunc =
    let compose (many : #seq<aval<AdaptiveFunc<'a>>>) =
        let many = Seq.toArray many


        if many |> Array.forall (fun m -> m.IsConstant) then
            let many = many |> Array.map AVal.force
            AdaptiveFunc<'a>(fun t (x : 'a) ->
                let mutable result = x
                for f in many do
                    result <- f.Run(t, result)
                result
            ) |> AVal.constant
        else
            AVal.custom (fun t ->
                let many = many |> Array.map (fun m -> m.GetValue t)
                
                AdaptiveFunc<'a>(fun t (x : 'a) ->
                    let mutable result = x
                    for f in many do
                        result <- f.Run(t, result)
                    result
                )

            )

    let mapMap (mapping : 'v -> aval<AdaptiveFunc<'a>>) (many : amap<'k, 'v>) =
        if many.IsConstant then
            let map = AMap.force many
            compose (map |> Seq.map (snd >> mapping))
        else
            let many =
                many 
                |> AMap.map (fun _ v -> mapping v)
                |> AMap.toAVal 
                |> AVal.map (HashMap.toValueArray)
            
            AVal.custom (fun token ->
                let many = many.GetValue(token) |> Array.map (fun c -> c.GetValue token)
                AdaptiveFunc<'a>(fun t (x : 'a) ->
                    let mutable result = x
                    for f in many do
                        result <- f.Run(t, result)
                    result
                )
            )

module GamepadController =

    let look (angularVelocity : aval<float>) (time : aval<System.DateTime>) (g : IGamepad) =
        angularVelocity |> AVal.map (fun speed ->
            let mutable tLast = None
            AdaptiveFunc(fun t (cam : CameraView) ->
                let d = g.RightStick.GetValue t
                if d = V2d.Zero then
                    tLast <- None
                    cam
                else
                    let now = time.GetValue t
                    let dt =
                        match tLast with
                        | Some tl -> 
                            tLast <- Some now
                            now - tl
                        | None ->   
                            tLast <- Some now
                            System.TimeSpan.Zero
                    let trafo =
                        M44d.Rotation(cam.Right, float d.Y * speed * dt.TotalSeconds ) *
                        M44d.Rotation(cam.Sky, float d.X * -speed * dt.TotalSeconds   )

                    let newForward = trafo.TransformDir cam.Forward |> Vec.normalize
                    cam.WithForward(newForward)

            )

        )
        

    let fly (moveSpeed : aval<float>) (time : aval<System.DateTime>) (g : IGamepad) =
        let moveSpeed =
            g.RightTrigger |> AVal.bind (fun v ->
                if v = 0.0 then
                    moveSpeed
                else
                    moveSpeed |> AVal.map (fun s -> s / (10.0 ** v))
            )
        
        AVal.constant (
            let mutable tLast = None
            AdaptiveFunc(fun t (cam : CameraView) ->
                let d = g.LeftStick.GetValue t
                if d = V2d.Zero then
                    tLast <- None
                    cam
                else
                    let now = time.GetValue t
                    let dt =
                        match tLast with
                        | Some tl -> 
                            tLast <- Some now
                            now - tl
                        | None ->   
                            tLast <- Some now
                            System.TimeSpan.Zero
                    
                    let newLocation = 
                        cam.Location + moveSpeed.GetValue t * dt.TotalSeconds * (cam.Forward * float d.Y + cam.Right * float d.X)
                    cam.WithLocation(newLocation)

            )

        )
    
    let all (moveSpeed : aval<float>) (angularVelocity : aval<float>) (time : aval<System.DateTime>) (g : IGamepad) =
        AdaptiveFunc.compose [
            fly moveSpeed time g
            look angularVelocity time g
        ]


[<EntryPoint>]
let main args =
    Aardvark.Init()
    VulkanApplication.SetDeviceChooser (fun d -> d |> Array.findIndex (fun d -> d.Type = VkPhysicalDeviceType.DiscreteGpu))
    let app = new OpenGlApplication()
    let win = app.CreateGameWindow()


    let angularVelocity = AVal.constant 2.0
    let moveSpeed = cval 5.0

    let initial = CameraView.lookAt (V3d(3,4,5)) V3d.Zero V3d.OOI
    let view =
        AVal.integrate initial win.Time [
            CameraController.controlWSAD win.Keyboard win.Time
            CameraController.controlLookAround win.Mouse
            CameraController.controlPan win.Mouse
            CameraController.controlZoom win.Mouse
            CameraController.controllScroll win.Mouse win.Time

            win.Gamepads |> AdaptiveFunc.mapMap (fun pad ->
                AdaptiveFunc.compose [
                    GamepadController.fly moveSpeed win.Time pad
                    GamepadController.look angularVelocity win.Time pad
                ]
            )
           
        ]

    let proj =
        win.Sizes
        |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

    let trafos =
        AVal.constant [|
            for x in -5 .. 5 do
                for y in -5 .. 5 do
                    Trafo3d.Translation(V3d(x,y,0) * 2.0)
        |]

    let sg =
        Sg.box' C4b.White Box3d.Unit
        |> Sg.instanced trafos
        |> Sg.diffuseTexture DefaultTextures.checkerboard
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.diffuseTexture
            do! DefaultSurfaces.simpleLighting
        }
        |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
        |> Sg.projTrafo (proj |> AVal.map Frustum.projTrafo)

    

    win.RenderTask <-
        RenderTask.ofList [
            app.Runtime.CompileClear(win.FramebufferSignature, clear { color C4f.Red; depth 1.0; stencil 0 })
            app.Runtime.CompileRender(win.FramebufferSignature, sg)
        ]

    win.Run()


    0