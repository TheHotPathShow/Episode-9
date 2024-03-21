# [The HotPath Show - Episode 9](https://www.youtube.com/watch?v=nkMbVjm-BAI)


## 1. Preparing GameObject based AssetStore assets

Start by getting the asset from the AssetStore. In this case, we are using the ["Robot Kyle | URP" asset](https://assetstore.unity.com/packages/3d/characters/robots/robot-kyle-urp-4696) from the AssetStore.

Now, look through the asset and build a ***rough**** plan of Managed behaviors you want to build interop for. (or already have) E.g. this project:

| Supported                                                           | Unsupported                                                    |
|---------------------------------------------------------------------|----------------------------------------------------------------|
| - Animator<br/>- Animation Events<br/>- Input System (C# generated) | - Cinemachine<br/>- UI<br/>- Input System (Input Action on MB) |

Also look out for any simplifications you can make to the asset. Make it just do what you need it to do. Nothing more.

To fulfill this first step, [the following changes were made to the project](https://github.com/TheHotPathShow/Episode-9/commit/a046a7bac3be816f04a972673d382dc01803b245):
- Removed the Mobile UI.
- Removed the `URPWizard`.
- Removed ability to push objects.
- Commited to only using the new Input System.
- Changed the Input System to use the generated C# code version.
- Character should only contain `Animator` on GameObjects that are separate to the root.
  - Kyle normally has 2 `Animator`s, one for the character, and one for the underlying model on the player
- Removed duplicate URP settings.
- [Removed Cinemachine.](https://github.com/TheHotPathShow/Episode-9/commit/15b01630b0da5ec37c0a71c4f76a8cc0433432ac)
- [Moved kyle scene to the root of the project.](https://github.com/TheHotPathShow/Episode-9/commit/84d0cea1443c95d2c335bc092eb25bfe237216e9) ([and fixed lighting](https://github.com/TheHotPathShow/Episode-9/commit/34c5b36f9704bcced00b00469198cd2af08ae016))
- [Ensure build settings are correct.](https://github.com/TheHotPathShow/Episode-9/commit/40e032b3d7562a08e3f2e9954f16b7d50e7e57d3) (Make a build to test)

## 2. Building Animation interop

When building any GameObject to Entities interop, you need to consider which case you're dealing with:
- A. Your Managed Behaviour is heavily tied to an object (e.g. `Animator`)
- B. Your Managed Behaviour is heavily tied to the scene (e.g. `Input System` or `UI`)

### Applies to both:
I generally recommend having your ECS Systems be the owner of the behavior. This means that the system is responsible for updating the behavior (like activating an Animation), and the behavior is only partly responsible for providing the data the system needs to act on. 

When pulling Managed data from an ECS System, you need the managed reference first. This is the main crux of the problem. And where the approach differs between A and B.

Now, I won't be covering B this time, but generally `static`'s or ECS Singletons work well for this. (See [Episode 3](https://www.youtube.com/watch?v=xDkfGlUYOAE&t=1769s) for an example of this in action.)
### Applies to A:
A is a bit more complex than B, as you need to consider how you're going to handle tying an Entity to a GameObject.
Luckily, Entities has managed components. Not only are `IComponentData` on a class a managed component, but so is any Unity Object that inherits from `UnityEngine.Object`. This means that you can have any `GameObject` component on an Entity.

That said, you still need to get the `GameObject` to the `Entity` in the first place. 
Now, SubScenes can only store entities in the SubScene hierarchy. 
This is a problem, because you can't have a `GameObject` in a SubScene, and you can't store a reference outside of the SubScene.
One exception to this rule is `UnityObjectRef<T>` which is a managed reference to a `UnityEngine.Object` that can be stored in a SubScene. Importantly for us, it can store a reference to a GameObject prefab.

So, here's the plan:
1. Create a `GameObject` prefab that contains the `Animator` component, along with the mesh it affects.
2. Create an empty object that gets baked in the SubScene that will contain a `UnityObjectRef<GameObject>` that references the prefab.
3. Create a PlayMode System that will `GameObject.Instantiate` the prefab, and attach the `Animator` to the `Entity` that the system is working on.
4. Sync the game object's transform to the entity's transform.
5. Use a cleanup component to destroy the `GameObject` when the entity is destroyed.
6. Realize edit mode has no visual representation of the `GameObject` prefab.
7. Create an EditMode System (for more detail [Episode 7](https://www.youtube.com/watch?v=-RKkUPk3WlM)) that will create an entity representation of the `GameObject` prefab.
8. Realize the EditMode visualization is not the same as the PlayMode visualization. He's laying down, but he should be standing up.
9. Create [a shader that supports Entities Graphics drawing SkinnedMeshes correctly](https://github.com/TheHotPathShow/Episode-9/commit/b5422b4129bdb2940d4888d6469122c678458c4b). Put it on Kyle.

### Result of [all this](https://github.com/TheHotPathShow/Episode-9/commit/dac4bd8149aa90623a5cff34a5d15a45c707ed7b), let's you write code like this:
```cs
var animator = SystemAPI.ManagedAPI.GetComponent<Animator>(characterData.AnimationEntity);
animator.SetBool(AnimIDJump, characterInput.Jump);
```

## 3. Importing the [`com.unity.charactercontroller` package](https://docs.unity3d.com/Packages/com.unity.charactercontroller@latest)
Set aside the animation interop for now, and focus on the character controller.
Start by [importing the package. And the sample files.](https://github.com/TheHotPathShow/Episode-9/commit/ef34ed31ec4718a66ad175ef8953e5f931a9ec59)
Then, the samples simply give a small example implementing the package. So let's look at the sample files and see what you can do to make it work, and start moving.
In this case, we need to:
1. Use [all the prefabs found in `/Samples/Character Controller/1.1.0-exp.10/Standard Characters/ThirdPerson/Prefabs`](https://github.com/TheHotPathShow/Episode-9/tree/ef34ed31ec4718a66ad175ef8953e5f931a9ec59/Assets/Samples/Character%20Controller/1.1.0-exp.10/Standard%20Characters/ThirdPerson/Prefabs), by putting them in the subscene.
2. Assign the `ThirdPersonCharacter` prefab to the matching field on `ThirdPersonPlayer` prefab.
3. Assign the `OrbitCamera` prefab to the matching field on `ThirdPersonPlayer` prefab.
4. Sync the Main Camera to the `OrbitCamera` prefab. By adding the component `MainGameObjectCamera` to the camera, and `MainEntityCameraAuthor
ig` to the `OrbitCamera` prefab.
5. Press play and see the character controller in action.

[The result of following those steps can be seen here](https://github.com/TheHotPathShow/Episode-9/commit/ef34ed31ec4718a66ad175ef8953e5f931a9ec59).

For this project I also chose to clean up the project a bit more to make it easier to follow along with the stream. This includes:
- [Delete FirstPerson assets](https://github.com/TheHotPathShow/Episode-9/commit/0fb6b4b39814e20fb86bbd78002625c124d341a4)
- [Jobs made Main Thread (easier to explain on stream)](https://github.com/TheHotPathShow/Episode-9/commit/a6742192133829c487981486a7276e8fabb6110c)
- [`ThirdPersonPlayer` becomes one with `ThirdPersonCharacterData`, and simplified camera sync down to one script `SyncWithEntityOrbitCamera`](https://github.com/TheHotPathShow/Episode-9/commit/35d162cccc00c5f860ae06bb4b6407acca3f5f58)
- [Removed some usages of `WithEntityAccess`](https://github.com/TheHotPathShow/Episode-9/commit/3d05b98c4772fb3a261423506a3c9db860633c94)
- [Renamed `Aspect.CharacterComponent` to `Aspect.CharacterData`](https://github.com/TheHotPathShow/Episode-9/commit/f609f076146c742dadde4ff52270e56028aca553)
- [Renamed `CharacterControl` to `CharacterInput` and `ThirdPersonCharacterControl` to `ThirdPersonCharacterInput`](https://github.com/TheHotPathShow/Episode-9/commit/06087323d16a11e432b6e2672fb84c099f77a199)

All of the above changes don't change the functionality of the project, but make it easier to follow along with the stream.

## 4. Merging the two projects

Now that we have both the character controller and the animation interop working, we can start merging the two projects.
First, made a few tweaks to the character controller to build confidence in how the character controller works:
1. [Locked the mouse cursor and hid it.](https://github.com/TheHotPathShow/Episode-9/commit/e5b5e4a8883e3ee22148415d8ac0b0b43190d525)
2. [Implemented sprint and changed to use the C# generated input system.](https://github.com/TheHotPathShow/Episode-9/commit/eb9abb99536d5e3e8d13b8b9d21550868b55d9d9)
3. [Made orbit camera ignores the pole.](https://github.com/TheHotPathShow/Episode-9/commit/04f71afeb1eb899ddb22b6deff52ff58c0903ad8)

[Then, I started merging the two projects by](https://github.com/TheHotPathShow/Episode-9/commit/e5b5e4a8883e3ee22148415d8ac0b0b43190d525):
1. Deleting the capsule from the `ThirdPersonCharacter` prefab.
2. Moving the empty game object that contains the `UnityObjectRef<GameObject>` to the `ThirdPersonCharacter` prefab.
3. Voila! The character controller is now moving the character with the model, that has an `Animator` attached to it.

Implementing animation is then as simple as [this commit](https://github.com/TheHotPathShow/Episode-9/commit/0fbf9cc7e461d554c0f459fc1f7bc01ac59fc98d).

Connecting animation events is as simple as [this commit](https://github.com/TheHotPathShow/Episode-9/commit/405c0ac681718768d3feb9f8175f988d5feab596).

Written by: Dani K Andersen ([@dani485b](https://twitter.com/dani485b))