using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.VisualScripting;
using UnityEngine;

public class HandLayout : MonoBehaviour
{
    public List<GameObject> cards = new();
    public float yValue = 1f;
    public float spacing = 0.67f;

    private readonly float cardWidth = 0.87f;
    void Start()
    {
        UpdateVariables();
        //InvokeRepeating(nameof(UpdateVariables), 0, 0.25f);
    }

    public void UpdateVariables()
    {
        cards.Clear();

        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf)
            {
                cards.Add(child.gameObject);
            }
        }

        int sortingOrder = 1;
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].GetComponent<SpriteRenderer>().sortingOrder = sortingOrder + i;
            cards[i].transform.localPosition = new Vector3(cards[i].transform.localPosition.x, yValue, 0);
        }
        if (cards.Count > 0)
        {
            UpdateCardsSpacing(spacing);
        }
    }

    private void UpdateCardsSpacing(float spacing)
    {
        int numOfCards = cards.Count;
        int middleCardIndex;
        float cardsMeanXPosition = GetAnchorXPosition();

        if (numOfCards % 2 == 0)
        {
            // Update middle index (left one)
            middleCardIndex = (numOfCards / 2) - 1;

            // Check if middle cards requires repositioning
            if (cards[middleCardIndex].transform.localPosition.x - cards[middleCardIndex + 1].transform.localPosition.x != spacing)
            {
                cards[middleCardIndex].transform.localPosition = new Vector3(cardsMeanXPosition - (spacing / 2), cards[middleCardIndex].transform.localPosition.y, 0);
                cards[middleCardIndex + 1].transform.localPosition = new Vector3(cardsMeanXPosition + spacing / 2, cards[middleCardIndex + 1].transform.localPosition.y, 0);
            }

            // Handle second half of deck
            for (int i = middleCardIndex + 1; i < cards.Count - 1; i++)
            {
                cards[i + 1].transform.localPosition = new Vector3(cards[i].transform.localPosition.x + spacing, cards[i + 1].transform.localPosition.y, 0);
            }

            // Handle first half of deck
            for (int i = middleCardIndex; i > 0; i--)
            {
                cards[i - 1].transform.localPosition = new Vector3(cards[i].transform.localPosition.x - spacing, cards[i - 1].transform.localPosition.y, 0);
            }
        }

        else if (numOfCards % 2 != 0)
        {
            // Update middle index
            middleCardIndex = numOfCards / 2;

            // Check if middle card requires repositioning
            if (cards[middleCardIndex].transform.localPosition.x != cardsMeanXPosition)
            {
                cards[middleCardIndex].transform.localPosition = new Vector3(cardsMeanXPosition, cards[middleCardIndex].transform.localPosition.y, 0);
            }

            if (cards.Count > 1)
            {
                // Handle second half of deck
                for (int i = middleCardIndex; i < cards.Count - 1; i++)
                {
                    cards[i + 1].transform.localPosition = new Vector3(cards[i].transform.localPosition.x + spacing, cards[i + 1].transform.localPosition.y, 0);
                }

                // Handle first half of deck
                for (int i = middleCardIndex; i > 0; i--)
                {
                    cards[i - 1].transform.localPosition = new Vector3(cards[i].transform.localPosition.x - spacing, cards[i + 1].transform.localPosition.y, 0);
                } 
            }
        }
    }
    private float GetAnchorXPosition() // May also return the y position, but is used solely to set the x position of the cards
    {
        if (transform.parent.rotation.eulerAngles.z == 90 || transform.parent.rotation.eulerAngles.z == 270)
        {
            return transform.parent.localPosition.y - (cardWidth / 2);
        }
        return transform.parent.localPosition.x - (cardWidth / 2);
    }
    public void ClearDeck()
    {
        cards.Clear();

        GameObject[] allChildren = new GameObject[transform.childCount];

        for (int i = 0; i < transform.childCount; i++)
        {
            allChildren[i] = transform.GetChild(i).gameObject;
        }

        for (int i = 0; i < allChildren.Length; i++)
        {
            Destroy(allChildren[i]);
        }
    }
}
