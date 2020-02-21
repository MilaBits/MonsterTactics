﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using Characters;
using Level;
using Sirenix.OdinInspector;
using UnityEngine;
using Utilities;

public class CharacterMover : MonoBehaviour
{
    [SerializeField, InlineEditor]
    private Character Character = default;

    private QuadTileMap tileMap;

    [Space]
    [SerializeField]
    private float stepTime = .5f;

    [SerializeField]
    private float jumpTime = 1f;

    [SerializeField]
    private AnimationCurve jumpCurve = default;

    [Space]
    [SerializeField]
    private GameObject ArrowMarkerPrefab = default;

    [SerializeField]
    private GameObject LineMarkerPrefab = default;

    private Queue<GameObject> LinePool = new Queue<GameObject>();
    private List<GameObject> visibleLines = new List<GameObject>();
    private GameObject Arrow;

    private GameObject GetLineSegmentFromPool()
    {
        if (LinePool.Count < 1) LinePool.Enqueue(Instantiate(LineMarkerPrefab));
        GameObject lineSegment = LinePool.Dequeue();
        lineSegment.SetActive(true);
        visibleLines.Add(lineSegment);
        return lineSegment;
    }

    private void StoreLineSegmentInPool(GameObject lineSegment)
    {
        lineSegment.SetActive(false);
        LinePool.Enqueue(lineSegment);
    }

    void Start()
    {
        Arrow = Instantiate(ArrowMarkerPrefab);
        Arrow.SetActive(false);

        tileMap = FindObjectOfType<QuadTileMap>();
    }

    [Button]
    private void MarkPossible()
    {
        foreach (QuadTile tile in tileMap.GetTilesInRange(
            tileMap.GetTile(Character.transform.position.ToVector2IntXZ()), Character.Data().move,
            Character.Data().stepLayerLimit,
            Character.Data().useRoughness))
        {
            tile.ToggleViableMarker(true);
        }
    }

    private void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Input.GetButtonDown("Fire1"))
        {
            if (Physics.Raycast(ray, out hit, 100, LayerMask.GetMask("Viable Marker")))
            {
                List<QuadTile> path = hit.transform.GetComponentInParent<QuadTile>().ChainToList();

                path.Reverse();
                Move(path);
                DrawPath(hit.transform.GetComponentInParent<QuadTile>());
            }
        }
    }

    private void DrawPath(QuadTile target)
    {
        List<QuadTile> path = target.ChainToList();
        path.Reverse();

        ClearLines();

        Arrow.SetActive(true);
        Arrow.transform.position = target.PositionWithHeight();
        RotateMarkerBasedOnDirection(Arrow.transform,
            target.transform.position.ToVector2IntXZ(),
            target.pathFindingData.cameFrom.transform.position.ToVector2IntXZ());
        for (int i = 0; i < path.Count - 1; i++)
        {
            GameObject segment = GetLineSegmentFromPool();
            segment.transform.position = path[i].PositionWithHeight();
            segment.name = $"segment {i}.{1}";
            RotateMarkerBasedOnDirection(segment.transform,
                path[i].transform.position.ToVector2IntXZ(),
                path[i].pathFindingData.cameFrom.transform.position.ToVector2IntXZ());

            GameObject segment2 = GetLineSegmentFromPool();
            segment2.transform.position = path[i].PositionWithHeight();
            segment2.name = $"segment {i}.{2}";
            RotateMarkerBasedOnDirection(segment2.transform,
                path[i].transform.position.ToVector2IntXZ(),
                path[i + 1].transform.position.ToVector2IntXZ());
        }
    }

    private void ClearLines()
    {
        Arrow.SetActive(false);
        foreach (GameObject visibleLine in visibleLines)
        {
            StoreLineSegmentInPool(visibleLine);
        }
    }

    private void RotateMarkerBasedOnDirection(Transform marker, Vector2Int curr, Vector2Int prev)
    {
        Vector2Int dir = curr - prev;
        if (dir.Equals(Vector2Int.up)) marker.rotation = Quaternion.Euler(0, 180, 0);
        else if (dir.Equals(Vector2Int.right)) marker.rotation = Quaternion.Euler(0, 270, 0);
        else if (dir.Equals(Vector2Int.down)) marker.rotation = Quaternion.Euler(0, 0, 0);
        else if (dir.Equals(Vector2Int.left)) marker.rotation = Quaternion.Euler(0, 90, 0);
    }

    private void Move(List<QuadTile> path) => StartCoroutine(MoveAlongPath(path, stepTime));

    private IEnumerator MoveAlongPath(List<QuadTile> path, float stepTime)
    {
        Character.ChangeAnimation("Walk");
        for (int i = 0; i < path.Count; i++)
        {
            yield return StartCoroutine(
                TakePathStep(path[i].PositionWithHeight(), stepTime));
        }

        MarkPossible();
        ClearLines();
        Character.ChangeAnimation("Idle");
        Character.FlipCharacter(false);
    }

    private IEnumerator TakePathStep(Vector3 target, float stepDuration)
    {
        Vector3 start = Character.transform.position;
        FlipCharacterBasedOnDirection(target, start);

        bool jump = start.y != target.y;

        float progress;

        if (jump)
        {
            stepDuration = jumpTime;
            Character.ChangeAnimation("Jump");
            yield return new WaitForSeconds(stepDuration * .33f);
            stepDuration *= .66f;
        }

        for (float elapsed = 0; elapsed < stepDuration; elapsed += Time.deltaTime)
        {
            progress = elapsed / stepDuration;
            float yOffset = jump ? jumpCurve.Evaluate(progress) : 0;

            Character.transform.position = Vector3.Lerp(start, target, progress) + Vector3.up * yOffset;
            yield return null;
        }

        Character.transform.position = target;
    }

    private void FlipCharacterBasedOnDirection(Vector3 target, Vector3 start)
    {
        Vector2Int direction = target.ToVector2IntXZ() - start.ToVector2IntXZ();
        if (direction.Equals(Vector2Int.up)) Character.FlipCharacter(true);
        else if (direction.Equals(Vector2Int.right)) Character.FlipCharacter(true);
        else if (direction.Equals(Vector2Int.down)) Character.FlipCharacter(false);
        else if (direction.Equals(Vector2Int.left)) Character.FlipCharacter(false);
    }
}