using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using TMPro;

/*
<summary>
This script receives accelerometer data from the Movesense sensor 
via UDP and visualizes it as a movement of a 3D dice in Unity.

The system architecture consists of:
- Input: UDP stream from Python software.
- Processing: acceleration-based orientation estimation
- Output: Unity transform rotation and detected dice face.

NOTE: 
Only accelerometer data is used for visualization and face detection.
Gyroscope and magnetometer data are included in the UDP packets for future development.
</summary>
*/

public class MovesenseDice : MonoBehaviour
{
    // UDP communication
    UdpClient client;
    Thread receiveThread;

    public int port = 5005;

    //UI output for displaying detected dice face, mainly for testing purposes.
    public TMP_Text diceText;

    // Latest received accelerometer sample
    Vector3 acc = Vector3.zero;

    //Stability filtering parameters for dice face detection
    float stableTime = 0.15f;
    float confidenceThreshold = 0.75f;

    int stableFace = 1;
    int candidateFace = 1;
    float candidateStart = 0;

    /*
    <summary>
    Maps measurements from the physical dice to the virtual dice coordinate system in Unity.
    </summary>
    */
    static readonly Matrix4x4 SENSOR_TO_DICE = new Matrix4x4(
        new Vector4(-1,0,0,0),
        new Vector4(0,-1,0,0),
        new Vector4(0,0,-1,0),
        new Vector4(0,0,0,1)
    );

    void Start()
    {
        // Initialize UDP client and start background thread for receiving data.
        client = new UdpClient(port);

        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }
    // Background thread continously receives UDP packets.

    void ReceiveData()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);

        while (true)
        {
            try
            {
                byte[] data = client.Receive(ref anyIP);

                //First byte defines packet type (1 = IMU)

                int packetType = data[0];

                if (packetType == 1)
                {
                    /*
                    Binary layout assumption:
                    byte 0: packet type
                    byte 1-8: timestamp (double)
                    byte 9+: acclerometer payload (float x, y, z)
                    */
                    float ax = BitConverter.ToSingle(data, 9);
                    float ay = BitConverter.ToSingle(data, 13);
                    float az = BitConverter.ToSingle(data, 17);

                    acc = new Vector3(ax, ay, az);
                }
            }
            catch { }
        }
    }

    void Update()
    {
        // Normalize accelerometer vector.
        // Assumes the device is stationary, so acceleration is resembles the direction of gravity.
        Vector3 a = acc.normalized;

        // Convert sensor measurements into dice coordinate system.
        Vector3 diceAcc = SENSOR_TO_DICE.MultiplyVector(a);

        // Gravity direction is opposite to measured acceleration, so the vector is inverted.
        Vector3 gravity = -diceAcc;

        // Compute rotation that aligns Unity's up vector with estimated gravity direction.
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, gravity);

        transform.rotation = rot;

        // Determine which face of the dice is currently facing upward based on gravity's direction.
        int face = GetFace(gravity);

        if(diceText != null)
            diceText.text = "" + face;
    }

    int GetFace(Vector3 gravity)
    {
        Vector3[] dirs =
        {
            new Vector3(0,0,1),
            new Vector3(0,0,-1),
            new Vector3(1,0,0),
            new Vector3(-1,0,0),
            new Vector3(0,1,0),
            new Vector3(0,-1,0)
        };

        int[] faces = {1,6,2,5,3,4};

        float bestDot = -1;
        int bestFace = stableFace;

        // Find direction most aligned with gravity vector.
        for(int i=0;i<dirs.Length;i++)
        {
            float d = Vector3.Dot(gravity, dirs[i]);

            if(d > bestDot)
            {
                bestDot = d;
                bestFace = faces[i];
            }
        }
        // Reject low-confidence measurements.
        if(bestDot < confidenceThreshold)
            return stableFace;

        // Temporal smoothing to avoid the jitter and to prevent the rapid swithing.
        if(bestFace == candidateFace)
        {
            if(Time.time - candidateStart > stableTime)
                stableFace = bestFace;
        }
        else
        {
            candidateFace = bestFace;
            candidateStart = Time.time;
        }

        return stableFace;
    }

    void OnApplicationQuit()
    {
        // Clean up network resources and background thread.
        if (receiveThread != null)
            receiveThread.Abort();

        if (client != null)
            client.Close();
    }
}