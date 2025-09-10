using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    [SerializeField] AudioSource correct;
    [SerializeField] AudioSource wrong;
    [SerializeField] AudioSource hit;
    [SerializeField] AudioSource Death;
    [SerializeField] AudioSource levelFinish;
    void Awake()
    {
        Instance = this;
    }
    public void CorrectAnswer()
    {
        correct.Play();
    }

    public void WrongAnswer()
    {
        wrong.Play();
    }


    public void Stun()
    {
        hit.Play();
    }


    public void Died()
    {
        Death.Play();
    }

    public void LevelOver()
    {
        levelFinish.Play();
    }
}
