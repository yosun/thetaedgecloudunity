using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ThetaCloudUnity{ 
	public class ImageMeshGenerator : MonoBehaviour
{
	public string apiUrl = "YOUR_API_URL";

	public IEnumerator Generate2d(Texture2D texture, string prompt, string negativePrompt)
	{
		string sessionHash = GenerateSessionHash();

		yield return UploadImage(texture, (sketchPath) =>
		{
			StartCoroutine(CreateEventSource(
				sessionHash,
				0,
				SketchToSketchPngBody(sketchPath, GetUrlFromPath(sketchPath), texture, sessionHash),
				(sketchPngPath) =>
				{
					StartCoroutine(CreateEventSource(
						sessionHash,
						1,
						SketchPngTo2dBody(sketchPngPath, GetUrlFromPath(sketchPngPath), texture, sessionHash, prompt, negativePrompt),
						(image2dPath) =>
						{
							StartCoroutine(CreateEventSource(
								sessionHash,
								2,
								Image2dToImage2d2Body(image2dPath, GetUrlFromPath(image2dPath), texture, sessionHash),
								(finalImagePath) =>
								{
									Debug.Log("Generated 2D image path: " + finalImagePath);
								}));
						}));
				}));
		});
	}

	private string GenerateSessionHash()
	{
		return System.Guid.NewGuid().ToString("N").Substring(0, 10);
	}

	private IEnumerator UploadImage(Texture2D texture, System.Action<string> callback)
	{
		byte[] fileData = texture.EncodeToPNG();
		string fileName = "sketch.png";

		WWWForm form = new WWWForm();
		form.AddBinaryData("files", fileData, fileName, "image/png");

		using (UnityWebRequest www = UnityWebRequest.Post($"{apiUrl}/upload", form))
		{
			yield return www.SendWebRequest();

			if (www.result != UnityWebRequest.Result.Success)
			{
				Debug.Log(www.error);
			}
			else
			{
				string response = www.downloadHandler.text;
				Debug.Log("Upload response: " + response);
				// Parse the response to get the path
				string path = ParseUploadResponse(response);
				callback(path);
			}
		}
	}

	private IEnumerator CreateEventSource(string sessionHash, int fnIndex, object body, System.Action<string> callback)
	{
		string eventSourceUrl = $"{apiUrl}/queue/join?fn_index={fnIndex}&session_hash={sessionHash}";

		using (UnityWebRequest www = UnityWebRequest.Get(eventSourceUrl))
		{
			yield return www.SendWebRequest();

			if (www.result != UnityWebRequest.Result.Success)
			{
				Debug.Log(www.error);
			}
			else
			{
				string response = www.downloadHandler.text;
				Debug.Log("EventSource response: " + response);
				// Parse the response to get the event ID
				string eventId = ParseEventSourceResponse(response);

				yield return QueueData(body, eventId, callback);
			}
		}
	}

	private IEnumerator QueueData(object body, string eventId, System.Action<string> callback)
	{
		string jsonBody = JsonUtility.ToJson(body);

		using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{apiUrl}/queue/data", jsonBody))
		{
			www.SetRequestHeader("Content-Type", "application/json");

			yield return www.SendWebRequest();

			if (www.result != UnityWebRequest.Result.Success)
			{
				Debug.Log(www.error);
			}
			else
			{
				string response = www.downloadHandler.text;
				Debug.Log("QueueData response: " + response);
				// Parse the response to get the path
				string path = ParseQueueDataResponse(response);
				callback(path);
			}
		}
	}

	private string GetUrlFromPath(string path)
	{
		return $"{apiUrl}/file={path}";
	}

	private object SketchToSketchPngBody(string sketchPath, string sketchUrl, Texture2D texture, string sessionHash)
	{
		return new
		{
			data = new[]
			{
				new
				{
				background = new
				{
				mime_type = "",
				orig_name = "background.png",
				path = sketchPath,
				size = texture.width * texture.height * 4,
				url = sketchUrl
				},
				composite = new
				{
				mime_type = "",
				orig_name = "composite.png",
				path = sketchPath,
				size = texture.width * texture.height * 4,
				url = sketchUrl
				}
				}
			},
			session_hash = sessionHash,
			fn_index = 0,
			trigger_id = 30,
			event_data = (object)null
		};
	}

	private object SketchPngTo2dBody(string sketchPath, string sketchUrl, Texture2D texture, string sessionHash, string prompt, string negativePrompt)
	{
		return new
		{
			data = new object[]
			{
				new
				{
				mime_type = (string)null,
				orig_name = "image.png",
				path = sketchPath,
				size = (long?)null,
				url = sketchUrl
				},
				"stablediffusionapi/rev-animated-v122-eol",
				"lllyasviel/control_v11p_sd15_lineart",
				512,
				512,
				true,
				1,
				prompt,
				negativePrompt,
				1,
				7.5,
				30,
				"DDIM",
				0,
				"Lineart"
			},
			event_data = (object)null,
			fn_index = 1,
			session_hash = sessionHash,
			trigger_id = 30
		};
	}

	private object Image2dToImage2d2Body(string image2dPath, string image2dUrl, Texture2D texture, string sessionHash)
	{
		return new
		{
			data = new object[]
			{
				new
				{
				mime_type = (string)null,
				orig_name = "image.png",
				path = image2dPath,
				size = (long?)null,
				url = image2dUrl
				},
				true,
				0.85
			},
			event_data = (object)null,
			fn_index = 2,
			session_hash = sessionHash,
			trigger_id = 30
		};
	}

	private string ParseUploadResponse(string response)
	{
		// Implement parsing logic to extract the path from the response
		return "uploads/sketch.png";
	}

	private string ParseEventSourceResponse(string response)
	{
		// Implement parsing logic to extract the event ID from the response
		return "event_id";
	}

	private string ParseQueueDataResponse(string response)
	{
		// Implement parsing logic to extract the path from the response
		return "generated_image.png";
	}
}

}
