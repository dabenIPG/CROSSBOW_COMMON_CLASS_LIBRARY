import cv2
import numpy as np
import sys
print("PYTHON VS:       " + sys.version)
print("NUMPY VS:        " + np.__version__)
print("OPENCV VS:       " + cv2.__version__)
print("CV2 path:        " + cv2.__file__)
print("CUDA DNN targets:", cv2.dnn.getAvailableTargets(cv2.dnn.DNN_BACKEND_CUDA))
print("CUDA devices:    ", cv2.cuda.getCudaEnabledDeviceCount())
