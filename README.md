> This is not an official Tilt Five product. Read [the disclaimer](#Disclaimer) for details.
# Jigsaw For Tilt Five
![Jigsaw Screenshot!](/Recordings/ScreenCap.gif)
</br><sub>Jigsaw Screencap - [Longer version here](https://github.com/KasHunt/T5Doodle_Jigsaw/raw/master/Recordings/ScreenCap.mp4)</sub>

## Description
A simple jigsaw application for Tilt Five.

You'll need Tilt Five Glasses to run this application. 
If you don't know what this is, visit the [Tilt Five website](https://tiltfive.com)
to discover the future of tabletop AR.  

This is hopefully the first in a number of programming 'doodles' (short, simple, 
unrefined projects).  This particular one took me ~14 hours. It's been a while
since I used Unity so hopefully future doodles will be quicker...

PRs/feedback/bugs welcome, though no guarantees that this project will get any
love after it's posted.

## Usage
### Menu
The cog icon brings up the settings menu:
- New Jigsaw - Create a new jigsaw from an image file
- Load Jigsaw - Load a previously created jigsaw (Saving is automatic)
- Options - Show controls for sound volume and wand arc length
- Quit - Exit the application

The jigsaw icon brings up the jigsaw menu:
- Reset - Move all of the pieces to their solved positions. (Pieces can bounce a bit so you mat need to click it twice)
- Randomize - Launch the pieces in random directions
- Toggle Hint - Toggle hint visibility

### Wand controls
| Control          | Action                                                                                          |
|------------------|-------------------------------------------------------------------------------------------------|
| Stick Left/Right | *(While not moving)* Free rotate pieces<br/>*(While snapping one piece)* Rotate 90° increments. | 
| Stick Up/Down    | Resize selection area                                                                           |
| 1 Button         | Flip pieces                                                                                     |
| 2 Button         | Toggle hint visibility                                                                          |
| Trigger (Held)   | Move selected pieces                                                                            |

### Snapping
If you move a single piece near to any other piece, it'll automatically 'snap' to match the rotation of the other piece.
It'll also snap to a side of the other piece. Note that it doesn't mean it's the correct orientation - just the closest
90° to the other piece. While it's snapping like this, thumbstick left/right will rotate it 90° increments.

## Future Ideas / Known Issues
- Add mixcast
- Support snapping groups together
- Undo/rewind
- Timed challenge mode
- Jigsaw of the day (online?)
- Leaderboard?
- Selectable surface material
- Remote multiplayer

## Development Time
- Approximately 14 hours (Including a rabbit hole of ballistics calculations)

## Tooling
- Unity 2021.3 **: Game Engine**
- JetBrains Rider 2023.2 **: IDE**
- Blender 3.4 **: 3D Model Creation**
- Adobe Illustrator 2023 **: Vector Image Editing**
- Adobe Photoshop 2023 **: Bitmap Image Editing**
- Adobe Audition 2023 **: Audio Editing**
- Adobe Premier Pro 2023 **: Video Editing**

## Disclaimer
This application was personally developed by Kasper John Hunt, who has a
professional association with Tilt Five, the producer of Tilt Five augmented
reality headset on which the application can be run. However, please be advised
that this application is a personal and independent project.

It is not owned, approved, endorsed, or otherwise affiliated with
Tilt Five in any official capacity.

The views, ideas, and content expressed within this application are solely those
of the creator and do not reflect the opinions, policies, or positions of Tilt Five.
Any use of the Tilt Five's name, trademarks, or references to its products is for
descriptive purposes only and does not imply any association or sponsorship by Tilt Five.

Users of this application should be aware that it is provided "as is" without any
warranties or representations of any kind. Any questions, comments, or concerns
related to this application should be directed to Kasper Hunt and not to Tilt Five.

## Copyright
Copyright 2023 Kasper John Hunt

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
