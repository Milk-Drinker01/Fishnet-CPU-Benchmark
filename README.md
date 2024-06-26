# Fishnet-CPU-Benchmark
 A benchmark to test the CPU performance of Fishnet networking engine

This benchmark will test 2 scenarios:
1) an integration of Kinematic Character controller, to test the performance of prediction v2, plus randomly moving server-sided objects
2) a raw test of the replication layer, using only randomly moving server-sided objects

Notes:
- Made with unity 2023.2 (specifically 2023.2.2f1), so that i could use Multiplayer Playmode to easily test functionality
- Made with fishnet 4.10, the latest version at the time the benchmark was written
- Tickrate: 33. Physics mode: disabled
- You will need to import Kinematic Character Controller: https://assetstore.unity.com/packages/tools/physics/kinematic-character-controller-99131
- The Fishnet integration of KCC was written by me, feel free to use it in your own projects
- The Move/Wander objects were written by Steak, and stolen from his bandwidth benchmark repo: https://github.com/StinkySteak/unity-netcode-benchmark
- All 100 clients connected from different computers



**EARLY RESULTS**

without anything to compare it to, these results are worthless. more information and testing to come!

100 clients (each with a predicted KCC player) and 100 server-sided move/wander objects:
Server tick time varied from 7 to 12 ms

![image](https://github.com/Milk-Drinker01/Fishnet-CPU-Benchmark/assets/59656122/054d3077-df5f-41b4-84ea-8b9ae0682362)

![image](https://github.com/Milk-Drinker01/Fishnet-CPU-Benchmark/assets/59656122/b98d4c27-c47f-4421-93f8-9a22da8e2d05)

![image](https://github.com/Milk-Drinker01/Fishnet-CPU-Benchmark/assets/59656122/a6969c95-45c6-4866-a7aa-c3afdd27e322)

100 clients (no player-owned objects) and 500 server-sided move/wander objects:
test not performed yet
