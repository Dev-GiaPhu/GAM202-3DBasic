Simple Fake Volume Fog

A lightweight, UV-free fog shader solution using world-space projection — perfect for mobile, VR, stylized games, and low-end hardware.

📌 Summary

Simple Fake Volume Fog is a fast, stylized fog shader that requires no UVs and works instantly on any mesh. It uses world-space math and triplanar projection to simulate soft volumetric fog without the performance cost of true volumetrics.

Ideal for:

Mobile & VR

Procedural levels

Stylized games

Scenes with unwrapped or auto-generated meshes

📘 Description

The Simple Triplanar Fog Shader creates a volumetric-looking fog effect using clever shader math instead of complex lighting or 3D textures. Because it uses world-space position instead of UVs, it works immediately on any 3D model, even ones without proper UVs.

It is built with performance in mind and is fully suitable for all Unity render pipelines (depending on your shader version).

This package is perfect for developers who want quick atmospheric depth without sacrificing FPS.

✨ Features

World-Space Fog
No UVs required — uses object position for fog fading.

Triplanar Blending
Ensures smooth coverage from all angles without stretching.

Alpha-Only Mode
Add fog on top of existing materials as a mask or overlay.

Performance First
Extremely lightweight, ideal for mobile & VR.

Drop-In Ready
Works out-of-the-box with any mesh. No baking or setup.

🎨 Customization Options
Property	Description
Fog Color	Controls tint of the fog effect
Fog Density	Adjusts thickness and opacity of the fog
World Scale	Controls how quickly fog fades through space
Blend Sharpness	Defines smoothness of triplanar blend
🧰 Technical Details

Shader-based fake volumetric fog

World-space projection

Triplanar blending

Supports alpha blending

Does not require custom meshes

Ultra-low performance cost

Ideal for real-time games and mobile hardware

✔ Best Use Cases

Procedural or unwrapped 3D models

Mobile / VR games needing atmosphere with low cost

Stylized fog effects

First-person & third-person exploration games

Prototypes and demos without proper UV setups

📁 Package Includes

Fog Shader (Triplanar / Alpha-Only variants)

Example Scene

User Guide PDF

Material presets