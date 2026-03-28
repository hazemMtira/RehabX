using UnityEngine;
using UnityEngine.AI;

public class AniamalWander : MonoBehaviour
{
    public float wanderRadius = 4f;
    public float wanderTime   = 3f;
    public Transform wanderCenter; // assign in Inspector — different for each animal

    NavMeshAgent agent;
    Animator     animator;
    float        timer;

    void Start()
    {
        agent    = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        timer    = wanderTime;

        // If no center assigned, default to starting position
        if (wanderCenter == null)
            wanderCenter = transform;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= wanderTime)
        {
            Vector3 newPos = RandomPoint(wanderCenter.position, wanderRadius);
            agent.SetDestination(newPos);
            timer = 0;
        }

        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    Vector3 RandomPoint(Vector3 center, float radius)
    {
        Vector3 randomPos = Random.insideUnitSphere * radius;
        randomPos += center;

        NavMeshHit hit;
        NavMesh.SamplePosition(randomPos, out hit, radius, NavMesh.AllAreas);
        return hit.position;
    }
}