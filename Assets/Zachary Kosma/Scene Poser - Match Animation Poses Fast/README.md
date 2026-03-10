# Match Animator Pose Editor

## Overview
The **Match Animator Pose Editor** is a custom Unity Editor tool that allows animators and developers to quickly apply a pose from an animation clip to an **Animator GameObject** in the Unity Editor. This can be useful for setting up character poses, previewing animations at specific frames, or manually tweaking animation poses before keyframing.

## Features
- Select an **Animation Clip** from an Animator Controller.
- Apply the **first frame pose** of the selected clip to the GameObject.
- Works non-destructively—animation clips remain unchanged.
- Supports **Undo functionality**, allowing users to revert changes.

## Installation
1. Create a new **Editor** folder inside your Unity project (if not already present).
2. Save the following script inside the **Editor** folder:
    - `MatchAnimatorPoseEditor.cs`
3. Attach an `Animator` component to a GameObject and assign an **Animator Controller** with Animation Clips.

## Usage
1. Select a GameObject with an `Animator` component in the Unity Editor.
2. Open the **Inspector** window.
3. Use the custom editor UI to choose an **Animation Clip** from the attached Animator Controller.
4. Click the **"Match Selected Animation Pose"** button.
5. The GameObject will adopt the first frame pose of the selected animation.
6. Use **Undo (Ctrl+Z / Cmd+Z)** if you want to revert the changes.

## How It Works
- The script retrieves animation clips from the assigned **Animator Controller**.
- It applies the first frame of the selected **Animation Clip** to the GameObject using:
  ```csharp
  clip.SampleAnimation(animator.gameObject, 0f);
  ```
- The object’s state is marked as changed using:
  ```csharp
  EditorUtility.SetDirty(animator.gameObject);
  ```
- Full object hierarchy is saved for Undo support:
  ```csharp
  Undo.RegisterFullObjectHierarchyUndo(animator.gameObject, "Match Animator Pose");
  ```

## Limitations
- **Does not modify animation clips permanently**—it only applies the pose to the GameObject.
- Currently **only samples the first frame** of an animation clip.
- Works **only in the Unity Editor** (not at runtime).

## Future Enhancements
- Support for applying a pose from **any frame** of an animation clip.
- Adding a timeline scrubber for finer control over pose selection.
- Option to bake sampled poses into an **AnimationClip**.

## License
The MIT License (MIT)

Copyright (c) 2011-2025 The Bootstrap Authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
---
Enjoy animating with ease! 🚀

