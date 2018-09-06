﻿using System.IO;
using TensorFlow;

namespace DNNTools
{
	public static class ImageUtil
	{
		// Convert the image in filename to a Tensor suitable as input to the Inception model.
		public static TFTensor CreateTensorFromImageFile (string file, TFDataType destinationDataType = TFDataType.Float)
		{
			var contents = File.ReadAllBytes (file);

			// DecodeJpeg uses a scalar String-valued tensor as input.
			var tensor = TFTensor.CreateString (contents);

			TFOutput input, output;
                        
			// Construct a graph to normalize the image
			using (var graph = ConstructGraphToNormalizeImage (out input, out output, destinationDataType)){
				// Execute that graph to normalize this one image
				using (var session = new TFSession (graph)) {
					var normalized = session.Run (
						inputs: new [] { input },
						inputValues: new [] { tensor },
						outputs: new [] { output });
					
					return normalized [0];
				}
			}
		}


        public static TFTensor CreateTensorFromBuffer(byte[] data, int imageWidth, int imageHeight, int numChannels, 
                                                      int resizeWidth, int resizeHeight,
                                                      TFDataType destinationDataType = TFDataType.Float)
        {
            var tensor = TFTensor.FromBuffer(new TFShape(imageHeight, imageWidth, numChannels), data, 0,
                imageWidth * imageHeight * numChannels);

            TFOutput input, output;

            // Construct a graph to normalize the image
            using (var graph = ConstructGraphToNormalizeRawImage(out input, out output, resizeWidth, resizeHeight, destinationDataType))
            {
                // Execute that graph to normalize this one image
                using (var session = new TFSession(graph))
                {
                    var normalized = session.Run(
                        inputs: new[] { input },
                        inputValues: new[] { tensor },
                        outputs: new[] { output });

                    return normalized[0];
                }
            }
        }

		// The inception model takes as input the image described by a Tensor in a very
		// specific normalized format (a particular image size, shape of the input tensor,
		// normalized pixel values etc.).
		//
		// This function constructs a graph of TensorFlow operations which takes as
		// input a JPEG-encoded string and returns a tensor suitable as input to the
		// inception model.
		private static TFGraph ConstructGraphToNormalizeImage (out TFOutput input, out TFOutput output, TFDataType destinationDataType = TFDataType.Float)
		{
            // Some constants specific to the pre-trained model at:
            // https://storage.googleapis.com/download.tensorflow.org/models/inception5h.zip
            //
            // - The model was trained after with images scaled to 224x224 pixels.
            // - The colors, represented as R, G, B in 1-byte each were converted to
            //   float using (value - Mean)/Scale.

            const int W = 640;
            const int H = 480;
            const float Mean = 117;
			const float Scale = 1;

			var graph = new TFGraph ();
			input = graph.Placeholder (TFDataType.String);

			output = graph.Cast (graph.Div (
				x: graph.Sub (
					x: graph.ResizeBilinear (
						images: graph.ExpandDims (
							input: graph.Cast (
								graph.DecodeJpeg (contents: input, channels: 3), DstT: TFDataType.Float),
							dim: graph.Const (0, "make_batch")),
						size: graph.Const (new int [] { W, H }, "size")),
					y: graph.Const (Mean, "mean")),
				y: graph.Const (Scale, "scale")), destinationDataType);
			
			return graph;
		}



        private static TFGraph ConstructGraphToNormalizeRawImage(out TFOutput input, out TFOutput output, int resizeWidth, int resizeHeight, 
                                                                 TFDataType destinationDataType = TFDataType.Float)
        {
            // Some constants specific to the pre-trained model at:
            // https://storage.googleapis.com/download.tensorflow.org/models/inception5h.zip
            //
            // - The model was trained after with images scaled to 224x224 pixels.
            // - The colors, represented as R, G, B in 1-byte each were converted to
            //   float using (value - Mean)/Scale.

            const float Mean = 117;
            const float Scale = 1;

            var graph = new TFGraph();
            input = graph.Placeholder(TFDataType.UInt8);

            output = graph.Cast(graph.Div(
                x: graph.Sub(
                    x: graph.ResizeBilinear(
                        images: graph.ExpandDims(
                            input: graph.Cast(input, DstT: TFDataType.Float),
                            dim: graph.Const(0, "make_batch")),
                        size: graph.Const(new int[] { resizeWidth, resizeHeight }, "size")),
                    y: graph.Const(Mean, "mean")),
                y: graph.Const(Scale, "scale")), destinationDataType);

            return graph;
        }



    }
}
