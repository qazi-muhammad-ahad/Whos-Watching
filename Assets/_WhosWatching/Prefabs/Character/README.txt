WHO'S WATCHING? — REFERENCE-INSPIRED CREATURE CHARACTERS

This set uses original, cute creature designs inspired by the supplied images: soft draped silhouettes, oversized dark eyes, tiny limbs, and playful expressions. The previous humanoid assets are separate from this set.

Files
- Whos_Watching_Creature_Characters.blend: editable Blender source with four individual rigs and animation actions.
- Blue_Male_Wisp_Cap.fbx and Red_Male_Wisp.fbx: legless ghost runners with a looping float and lower-drape wave animation.
- Blue_Female_Puff.fbx and Red_Female_Puff_Beanie.fbx: horned puff monsters with looping walk animations.
- Hat_Baseball_Cap.fbx and Hat_Beanie.fbx: separate interchangeable accessories.

Unity setup
- Metric units: use Scale Factor 1 (1 Blender unit = 1 metre).
- Each character FBX includes its own Generic rig, movement animation, and a separate `Buff_Dance` clip. Set the walk/float clip to Loop Time in Unity; leave `Buff_Dance` non-looping and trigger it when the buff action succeeds.
- Puff walk cycles include alternating leg and arm swings with a light body bounce. Wisp float cycles use a gentle continuous vertical bob with stationary capsule arms; only the four lower hem bones ripple in a smooth wave, so the motion stays floaty rather than reading as a walk.
- Buff dances return to the neutral pose. Puff taunts last about 1.7 seconds, with a bouncy side-to-side body sway, outward beckoning arm gestures, and a little shuffle; wisps stay airborne, bob and sway, lift their capsule arms, and ripple their hems without a walking cycle.
- Puff arms are single, curved meshes that start embedded in the torso and taper outward into rounded hand ends, avoiding separate shoulder or hand balls.
- The main body uses a Primary team material; arms and feet use a separate Accent team material where present. Keep those slots separate for runtime color selection.
- Face, eye highlights, horns, and hat materials are separate neutral materials.
- Hats are centered around their attachment pivot. Parent the hat root to a head/top attachment point, then adjust the local position to fit.
- Models use simple meshes and no texture maps or transparency effects.
