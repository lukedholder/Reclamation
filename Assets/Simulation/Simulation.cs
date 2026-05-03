// The core simulation. GameManager creates one of these and calls Update
// 20 times per second. All game logic will live in here, separate from Unity.

public class Simulation
{
    public int Tick { get; private set; }

    public void Update()
    {
        Tick++;
    }
}