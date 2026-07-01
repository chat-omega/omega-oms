# -*- coding: utf-8 -*-
"""
Created on Tue Aug 15 22:06:51 2023

@author: lucas
"""
import pickle as pk
import numpy as np


def bucket_to_theo(bucket):
    if bucket == 0:
        return .1
    elif bucket == 1:
        return .1
    elif bucket == 2:
        return .15
    elif bucket == 3:
        return .2
    elif bucket == 4:
        return .3
    elif bucket == 5:
        return .4
    elif bucket == 6:
        return .5
    elif bucket == 7:
        return .75
    elif bucket == 8:
        return 1
    else:
        return 1.3

def get_saved_model():
    k_neighbors = pk.load(open("k_nearest_goog_meta_googl.pkl", "rb"))
    return k_neighbors

# [ "leg_delta.1","leg_delta.2", 'open_side', 'width', "C_P", "dte1", "dte2"]  + underlying

def underlying_conversion(symbol):
    if symbol == "GOOG":
        return [1,0,0,0]
    elif symbol == "GOOGL":
        return [0,1,0,0]
    elif symbol == "META":
        return [0,0,1,0]
    elif symbol == "MSFT":
        return [0,0,0,1]

def edge_to_theo(delta_leg_1, deltaa_leg_2, side, width, C_P, dte1, dte2, underlying):
    x_var = list((delta_leg_1, deltaa_leg_2, side, width, C_P, dte1, dte2)) + underlying_conversion(underlying)
    x_var = np.array(x_var)
    model  = get_saved_model()
    bucket = model.predict([x_var])
    edge = bucket_to_theo(bucket)
    return edge