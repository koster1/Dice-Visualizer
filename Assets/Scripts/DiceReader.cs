using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using TMPro;

public class DiceReader : MonoBehaviour
{
    // UDP communication
    private UdpClient client;
    private Thread receiveThread;

    public int port = 5005;

    // UI
    public TMP_Text diceText;
    public TMP_Text receiveButtonText;
    public TMP_Text exitText;

    // Latest accelerometer sample
    private Vector3 acc = Vector3.zero;

    // Receiver state
    private bool receiving = false;

    // Stability filtering parameters
    private float stableTime = 0.15f;
    private float confidenceThreshold = 0.75f;

    private int stableFace = 1;
    private int candidateFace = 1;
    private float candidateStart = 0;

    static readonly Matrix4x4 SENSOR_TO_DICE = new Matrix4x4(
        new Vector4(-1, 0, 0, 0),
        new Vector4(0, -1, 0, 0),
        new Vector4(0, 0, -1, 0),
        new Vector4(0, 0, 0, 1)
    );

    void Start()
    {
    QualitySettings.SetQualityLevel(0);
    QualitySettings.vSyncCount = 0;
    Application.targetFrameRate = 60;

        if (receiveButtonText != null)
            receiveButtonText.text = "Start Receiving";
    }

    public void ToggleReceiving()
    {
        if (receiving)
        {
            StopReceiving();

            if (receiveButtonText != null)
                receiveButtonText.text = "Start Receiving";
        }
        else
        {
            StartReceiving();

            if (receiveButtonText != null)
                receiveButtonText.text = "Stop Receiving";
        }
    }

    public void StartReceiving()
    {
        if (receiving)
            return;

        try
        {
            client = new UdpClient(port);

            receiving = true;

            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log("UDP receiving started.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start UDP receiver: " + e.Message);
        }
    }

    public void StopReceiving()
    {
        if (!receiving)
            return;

        receiving = false;

        try
        {
            if (client != null)
            {
                client.Close();
                client = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(200);
                receiveThread = null;
            }

            Debug.Log("UDP receiving stopped.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to stop UDP receiver: " + e.Message);
        }
    }

    public void ExitToMenu()
    {
        StopReceiving();

        if (exitText != null)

        UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu");
    }

    void ReceiveData()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);

        while (receiving)
        {
            try
            {
                byte[] data = client.Receive(ref anyIP);

                if (data == null || data.Length < 21)
                    continue;

                int packetType = data[0];

                if (packetType == 1)
                {
                    float ax = BitConverter.ToSingle(data, 9);
                    float ay = BitConverter.ToSingle(data, 13);
                    float az = BitConverter.ToSingle(data, 17);

                    acc = new Vector3(ax, ay, az);
                }
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning("UDP receive error: " + e.Message);
            }
        }
    }

    void Update()
    {
        if (acc == Vector3.zero)
            return;

        Vector3 a = acc.normalized;

        Vector3 diceAcc = SENSOR_TO_DICE.MultiplyVector(a);

        Vector3 gravity = -diceAcc;

        Quaternion rot = Quaternion.FromToRotation(Vector3.up, gravity);

        transform.rotation = rot;

        int face = GetFace(gravity);

        if (diceText != null)
            diceText.text = face.ToString();
    }

    int GetFace(Vector3 gravity)
    {
        Vector3[] dirs =
        {
            new Vector3(0,0,1),   // 1
            new Vector3(1,0,0),   // 2
            new Vector3(0,1,0),   // 3
            new Vector3(0,-1,0),  // 4
            new Vector3(-1,0,0),  // 5
            new Vector3(0,0,-1)   // 6
        };

        int[] faces = { 1, 2, 3, 4, 5, 6 };

        float bestDot = -1f;
        int bestFace = stableFace;

        for (int i = 0; i < dirs.Length; i++)
        {
            float d = Vector3.Dot(gravity, dirs[i]);

            if (d > bestDot)
            {
                bestDot = d;
                bestFace = faces[i];
            }
        }

        if (bestDot < confidenceThreshold)
            return stableFace;

        if (bestFace == candidateFace)
        {
            if (Time.time - candidateStart > stableTime)
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
        StopReceiving();
    }
}