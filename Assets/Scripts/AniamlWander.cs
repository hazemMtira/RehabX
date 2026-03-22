using UnityEngine;
using UnityEngine.AI;

public class AniamlWander : MonoBehaviour
{
    public float wanderRadius = 4f;
    public float wanderTime = 3f;

    NavMeshAgent agent;
    Animator animator;
    float timer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        timer = wanderTime;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= wanderTime)
        {
            Vector3 newPos = RandomPoint(transform.position, wanderRadius);
            agent.SetDestination(newPos);
            timer = 0;
        }

        // THIS LINE CONTROLS ANIMATION
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
