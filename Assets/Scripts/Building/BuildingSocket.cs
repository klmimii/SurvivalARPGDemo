using System.Collections.Generic;
using UnityEngine;

public class BuildingSocket : MonoBehaviour
{
    private static readonly HashSet<BuildingSocket> AllSockets =
        new HashSet<BuildingSocket>();

    [SerializeField] private BuildingPieceType acceptedPieceType;

    private PlacedBuilding owner;
    private PlacedBuilding occupiedBy;

    public BuildingPieceType AcceptedPieceType => acceptedPieceType;
    public PlacedBuilding Owner => owner;
    public bool IsOccupied => occupiedBy != null;

    private void Awake()
    {
        owner = GetComponentInParent<PlacedBuilding>();
    }

    private void OnEnable()
    {
        AllSockets.Add(this);
    }

    private void OnDisable()
    {
        AllSockets.Remove(this);
        occupiedBy = null;
    }

    public bool CanAccept(
        BuildingPieceType pieceType,
        PlacedBuilding requestingBuilding = null)
    {
        bool free = occupiedBy == null || occupiedBy == requestingBuilding;
        return free && acceptedPieceType == pieceType;
    }

    public bool TryReserve(PlacedBuilding building)
    {
        if (building == null ||
            !CanAccept(building.Definition.pieceType, building))
        {
            return false;
        }

        occupiedBy = building;
        building.AttachTo(this);
        return true;
    }

    public void Release(PlacedBuilding building)
    {
        if (occupiedBy != building)
        {
            return;
        }

        occupiedBy = null;
        building.DetachFromSocket(this);
    }

    public static BuildingSocket FindBest(
        BuildingPieceType pieceType,
        Vector3 nearPosition,
        float radius,
        PlacedBuilding ignoredOwner = null)
    {
        BuildingSocket best = null;
        float bestSqrDistance = radius * radius;

        foreach (BuildingSocket socket in AllSockets)
        {
            if (socket == null ||
                socket.Owner == ignoredOwner ||
                !socket.CanAccept(pieceType))
            {
                continue;
            }

            float sqrDistance =
                (socket.transform.position - nearPosition).sqrMagnitude;

            if (sqrDistance <= bestSqrDistance)
            {
                best = socket;
                bestSqrDistance = sqrDistance;
            }
        }

        return best;
    }

    public static void ResetReservations()
    {
        foreach (BuildingSocket socket in AllSockets)
        {
            if (socket != null)
            {
                socket.occupiedBy = null;
            }
        }
    }

    public static void RebuildReservations(float tolerance = 0.12f)
    {
        ResetReservations();

        PlacedBuilding[] buildings =
            FindObjectsOfType<PlacedBuilding>();

        foreach (PlacedBuilding building in buildings)
        {
            if (building == null ||
                building.Definition == null ||
                !building.IsPlayerBuilt)
            {
                continue;
            }

            BuildingSocket socket = FindBest(
                building.Definition.pieceType,
                building.transform.position,
                tolerance,
                building);

            socket?.TryReserve(building);
        }
    }
}