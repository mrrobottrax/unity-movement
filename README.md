# What is this?
Unity has an issue. Box cast returns incorrect normal vectors against some mesh colliders, seeming most prominent with Probuilder meshes. This repository contains a script that fixes this behaviour for good with its own box cast replacement function that calculates its own normal vector.

## Before
(Images/normal_issue.webp)

## After
(Images/normal_fix.webp)
