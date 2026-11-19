# Tuni Lab

The software uniting all devices in some lab in Tampere University into a single service. Can

- stream data
  - also to a log file
- stream events 
- execute requests

## Supported devices

- Leap Motion
- Tobii EyeX
- VI myGaze
- Tension R Belts
- CARLA telemetry
- Simucube active pedals 
- Simucube Tahko steering wheel
- StreamDeck keyboard
- Web-cameras
- Joysticks and gamepads
- Screens
- Sound outputs

## Projects

- Core: Definitions of gRPC services
- Server: gRPC server. 
- Dashboard: An example of gRPC client
- Demo/BeltsBrakes: Tension R Belts are controlled using CARLA's telemetry
