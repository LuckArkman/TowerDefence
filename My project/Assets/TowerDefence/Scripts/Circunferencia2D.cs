using System.Collections.Generic;
using TowerDefence.Scripts;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class Circunferencia2D : MonoBehaviour
{
    [Header("Configurações da Circunferência")]
    public List<Vector3> spawnpoints = new();
    public float diametro = 10f;
    public int segmentos = 100;
    public float espessura = 0.1f;

    [Header("Instanciação")]
    public GameObject objetoParaInstanciar; // Prefab a ser instanciado em cada ponto
    public bool instanciarApenasNaInicializacao = true;

    private LineRenderer lineRenderer;
    private CircleCollider2D circleCollider;
    void Start()
    {
        Singleton._Instance.circunferencia = this;
        DesenharCircunferencia();
        ConfigurarCollider();
    }

    void Update()
    {
        DetectarClique();
    }

    void DesenharCircunferencia()
    {
        spawnpoints.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        float raio = diametro / 2f;
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = segmentos + 1;
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.widthMultiplier = espessura;

        Vector3[] pontos = new Vector3[segmentos + 1];

        for (int i = 0; i <= segmentos; i++)
        {
            float angulo = Mathf.Deg2Rad * ((float)i / segmentos * 360f);
            float x = Mathf.Cos(angulo) * raio;
            float y = Mathf.Sin(angulo) * raio;
            Vector3 pontoLocal = new Vector3(x, y, 0f);
            pontos[i] = pontoLocal;

            if (objetoParaInstanciar != null && instanciarApenasNaInicializacao)
            {
                Vector3 pontoMundo = transform.TransformPoint(pontoLocal);
                spawnpoints.Add(pontoMundo);
                Instantiate(objetoParaInstanciar, pontoMundo, Quaternion.identity, transform);
            }
        }

        lineRenderer.SetPositions(pontos);
    }

    void ConfigurarCollider()
    {
        float raio = diametro / 2f;
        circleCollider = GetComponent<CircleCollider2D>();
        circleCollider.radius = raio;
        circleCollider.isTrigger = true;
    }

    void DetectarClique()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 pontoClique = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(pontoClique, Vector2.zero);

            if (hit.collider != null && hit.collider.gameObject == this.gameObject)
            {
                Debug.Log("Circunferência clicada!");
            }
        }
    }
}
