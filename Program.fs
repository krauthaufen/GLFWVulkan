open FSharp.Data.Adaptive
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Application
open Aardvark.SceneGraph
open Silk.NET.GLFW
open Glfw


[<EntryPoint>]
let main args =
    Aardvark.Init()
    let app = new HeadlessVulkanApplication(false, ["VK_KHR_swapchain"; "VK_EXT_swapchain_colorspace"; "VK_MVK_moltenvk";"VK_EXT_metal_surface"; "VK_MVK_macos_surface"; "VK_KHR_surface"], fun _ -> ["VK_EXT_metal_surface"; "VK_MVK_macos_surface"; "VK_KHR_surface"])

    let glfw = Application(app.Runtime)
    let win = 
        glfw.CreateWindow { 
            width = 1024
            height = 768
            title = "Yeah"
            focus = false
            resizable = true
            vsync = true
            transparent = false
            opengl = false
            physicalSize = false
            samples = 1
        }

    let view =
        CameraView.lookAt (V3d(3,4,5)) V3d.Zero V3d.OOI
        |> DefaultCameraController.control win.Mouse win.Keyboard win.Time

    let proj =
        win.Sizes
        |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

    let sg =
        Sg.box' C4b.White Box3d.Unit
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.simpleLighting
        }
        |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
        |> Sg.projTrafo (proj |> AVal.map Frustum.projTrafo)

    

    win.RenderTask <-
        RenderTask.ofList [
            app.Runtime.CompileClear(win.FramebufferSignature, clear { color C4f.Black; depth 1.0; stencil 0 })
            app.Runtime.CompileRender(win.FramebufferSignature, sg)
        ]

    glfw.Run(win)


    0