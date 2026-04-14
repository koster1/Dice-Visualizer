using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class UDPRWii : MonoBehaviour
{
    [Header("UDP Settings")]
    public int port = 5005;

    [Header("Rotation")]
    public float rotationSmooth = 10f;

    [Header("Movement by Orientation")]
    public float moveSpeed = 3f;
    public float tiltThreshold = 10f;   // Kuinka paljon pitää kallistaa ennen kuin liikkuu (asteina)
    public float movementSmooth = 8f;

    private UdpClient udpClient;
    private Thread receiveThread;

    private Quaternion latestRotation = Quaternion.identity;
    private bool newOrientation = false;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.linearDamping = 5f;
        rb.angularDamping = 5f;
        //rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         //RigidbodyConstraints.FreezeRotationZ;

        udpClient = new UdpClient(port);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("Orientation Drive Receiver started on port " + port);
    }

    void ReceiveData()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                if (data.Length == 0) continue;

                byte packetType = data[0];

                // Orientation paketti
                if (packetType == 2)
                {
                    int offset = 1 + 8;

                    float w = BitConverter.ToSingle(data, offset);
                    float x = BitConverter.ToSingle(data, offset + 4);
                    float y = BitConverter.ToSingle(data, offset + 8);
                    float z = BitConverter.ToSingle(data, offset + 12);

                    latestRotation = new Quaternion(x, y, z, w);
                    newOrientation = true;
                }
            }
            catch (Exception err)
            {
                Debug.Log(err.ToString());
            }
        }
    }

    void FixedUpdate()
    {
        if (!newOrientation) return;

        Quaternion targetRotation = ConvertToUnityRotation(latestRotation);

        // Päivitä objekti rotaatio
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSmooth * Time.fixedDeltaTime
        );

        // --- Movement based on tilt ---

        // Forward-suunnan projekti tasoon
        Vector3 forward = transform.forward;
        forward.y = 0f;

        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);

        if (tiltAngle < tiltThreshold)
        {
            // Ei liikettä jos sensori lähes pystysuorassa
            rb.linearVelocity = Vector3.Lerp(
                rb.linearVelocity,
                Vector3.zero,
                movementSmooth * Time.fixedDeltaTime
            );
            return;
        }

        Vector3 targetVelocity = forward.normalized * moveSpeed;

        rb.linearVelocity = Vector3.Lerp(
            rb.linearVelocity,
            targetVelocity,
            movementSmooth * Time.fixedDeltaTime
        );

        newOrientation = false;
    }

    Quaternion ConvertToUnityRotation(Quaternion q)
    {
        // Movesense → Unity koordinaattimuunnos
        return new Quaternion(-q.x, -q.z, -q.y, q.w);
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }
}