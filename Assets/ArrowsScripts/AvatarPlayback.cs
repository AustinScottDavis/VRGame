﻿using UnityEngine;
using System.Collections;
using System;
using System.IO;
using Oculus.Avatar;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public class AvatarPlayback : Photon.PunBehaviour
{
    class PacketLatencyPair
    {
        public byte[] PacketData;
        public float FakeLatency;
    };

    public GameObject PersonalCamera;
    public int TestPlayerID;
    public OvrAvatar LocalAvatar;
    public OvrAvatarRemoteDriver LoopbackAvatar;

    public GameObject enemyBody;

    [System.Serializable]
    public class SimulatedLatencySettings
    {
        [Range(0.0f, 0.5f)]
        public float FakeLatencyMax = 0.25f; //250 ms max latency

        [Range(0.0f, 0.5f)]
        public float FakeLatencyMin = 0.002f; //2ms min latency

        [Range(0.0f, 1.0f)]
        public float LatencyWeight = 0.25f;  // How much the latest sample impacts the current latency

        [Range(0, 10)]
        public int MaxSamples = 4; //How many samples in our window

        internal float AverageWindow = 0f;
        internal float LatencySum = 0f;
        internal LinkedList<float> LatencyValues = new LinkedList<float>();

        public float NextValue()
        {
            AverageWindow = LatencySum / (float)LatencyValues.Count;
            float RandomLatency = UnityEngine.Random.Range(FakeLatencyMin, FakeLatencyMax);
            float FakeLatency = AverageWindow * (1f - LatencyWeight) + LatencyWeight * RandomLatency;

            if (LatencyValues.Count >= MaxSamples)
            {
                LatencySum -= LatencyValues.First.Value;
                LatencyValues.RemoveFirst();
            }

            LatencySum += FakeLatency;
            LatencyValues.AddLast(FakeLatency);

            return FakeLatency;
        }
    };

    public SimulatedLatencySettings LatencySettings = new SimulatedLatencySettings();

    private int PacketSequence = 0;

    LinkedList<PacketLatencyPair> packetQueue = new LinkedList<PacketLatencyPair>();

    public byte MaxPlayersPerRoom = 2;


    void Start()
    {
        Debug.Log("here");
        PhotonNetwork.ConnectUsingSettings("1.1");
        
    }

    //public override void OnConnectedToMaster()
    //{
    //    Debug.Log("DemoAnimator/Launcher: OnConnectedToMaster() was called by PUN");
    //}

    public override void OnConnectionFail(DisconnectCause cause)
    {
        Debug.Log("OnConnectionFail");
    }

    public override void OnFailedToConnectToPhoton(DisconnectCause cause)
    {
        Debug.Log("OnConnectionFail");
    }

    public override void OnJoinedLobby()
    {
        // Debug.Log("OnJoinedLobby called by PUN");
        // Debug.Log(PhotonNetwork.CreateRoom("Test Room", new RoomOptions() { MaxPlayers = MaxPlayersPerRoom }, null));
        //RoomInfo[] info = PhotonNetwork.GetRoomList();
        //Debug.Log("printing rooms");
        // foreach (var i in info)
        // {
        //    Debug.Log(i.ToString());
        // }
        Debug.Log("OnConnectedtoMaster");
        PhotonNetwork.JoinRandomRoom();

        return;
    }

    public override void OnPhotonRandomJoinFailed(object[] codeAndMsg)   
    {
        Debug.Log("onphotonjoinroomfailed");
        PhotonNetwork.CreateRoom("Please Work", new RoomOptions() { MaxPlayers = MaxPlayersPerRoom }, null);

    }

    public override void OnJoinedRoom()
    {
       Debug.Log("made it hereere!");
        
       TestPlayerID = PhotonNetwork.room.PlayerCount;

        if (TestPlayerID == 1)
        {
            PersonalCamera.transform.position = new Vector3(-5.5f, 2, 0);
            LocalAvatar.transform.position = new Vector3(-5.5f, 2, 0);
            LoopbackAvatar.transform.position = new Vector3(2.5f, 2, 0);
        }

        if (TestPlayerID == 2)
        {
            PersonalCamera.transform.Rotate(0, 180, 0);
            PersonalCamera.transform.position = new Vector3(2.5f, 2, 0);
            LocalAvatar.transform.position = new Vector3(2.5f, 2, 0);
            LocalAvatar.transform.Rotate(0, 180, 0);
            LoopbackAvatar.transform.position = new Vector3(-5.5f, 2, 0);
            LoopbackAvatar.transform.Rotate(0, 180, 0);
        }
        
        Debug.Log("success");
        Debug.Log(PhotonNetwork.room.PlayerCount);

        System.Random rnd = new System.Random();
        PhotonNetwork.playerName = "Fool #" + rnd.Next();

        LocalAvatar.RecordPackets = true;
        LocalAvatar.PacketRecorded += OnLocalAvatarPacketRecorded;
        float FirstValue = UnityEngine.Random.Range(LatencySettings.FakeLatencyMin, LatencySettings.FakeLatencyMax);
        LatencySettings.LatencyValues.AddFirst(FirstValue);
        LatencySettings.LatencySum += FirstValue;


        // hack the avatar
        GameObject renderBody = enemyBody.transform.GetChild(1).gameObject;
        GameObject rootJnt = renderBody.transform.GetChild(0).gameObject;
        GameObject bodyJnt = rootJnt.transform.GetChild(0).gameObject;
        GameObject chestJnt = bodyJnt.transform.GetChild(0).gameObject;
        GameObject neckBase = chestJnt.transform.GetChild(0).gameObject;
        GameObject neckJnt = neckBase.transform.GetChild(0).gameObject;
        GameObject headJnt = neckJnt.transform.GetChild(0).gameObject;
        CapsuleCollider collider = headJnt.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = 0.11f;
        collider.height = 0.26f;

        RemoteCollider rc = headJnt.AddComponent<RemoteCollider>();
        rc.gameControl = gameObject;
    }

   /* public override void OnJoinedLobby()
    {
        Debug.Log("On joined lobby");
        PhotonNetwork.CreateRoom("Test Room", new RoomOptions() { MaxPlayers = MaxPlayersPerRoom }, null);
        Debug.Log(PhotonNetwork.countOfRooms);

    }
    */
    void OnLocalAvatarPacketRecorded(object sender, OvrAvatar.PacketEventArgs args)
    {
        using (MemoryStream outputStream = new MemoryStream())
        {
            BinaryWriter writer = new BinaryWriter(outputStream);

            var size = CAPI.ovrAvatarPacket_GetSize(args.Packet.ovrNativePacket);
            byte[] data = new byte[size];
            CAPI.ovrAvatarPacket_Write(args.Packet.ovrNativePacket, size, data);

            writer.Write(PacketSequence++);
            writer.Write(size);
            writer.Write(data);

            //SendPacketData(outputStream.ToArray());

            PhotonView photonView = PhotonView.Get(this);
            //Debug.Log(photonView);
            System.Object[] arr = { outputStream.ToArray() };
            photonView.RPC("ReceivePacketData", PhotonTargets.Others, arr);
        }
    }


    [PunRPC]
    void ReceivePacketData(byte[] data)
    {
        using (MemoryStream inputStream = new MemoryStream(data))
        {
            BinaryReader reader = new BinaryReader(inputStream);
            int sequence = reader.ReadInt32();

            int size = reader.ReadInt32();
            byte[] sdkData = reader.ReadBytes(size);

            IntPtr packet = CAPI.ovrAvatarPacket_Read((UInt32)data.Length, sdkData);
            LoopbackAvatar.GetComponent<OvrAvatarRemoteDriver>().QueuePacket(sequence, new OvrAvatarPacket { ovrNativePacket = packet });
        }
    }


    void Update()
    {
        if (packetQueue.Count > 0)
        {
            List<PacketLatencyPair> deadList = new List<PacketLatencyPair>();
            foreach (var packet in packetQueue)
            {
                packet.FakeLatency -= Time.deltaTime;

                if (packet.FakeLatency < 0f)
                {
                    ReceivePacketData(packet.PacketData);
                    deadList.Add(packet);
                }
            }

            foreach (var packet in deadList)
            {
                packetQueue.Remove(packet);
            }
        }
    }
}