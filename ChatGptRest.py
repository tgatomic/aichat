from flask import Flask, request, jsonify, make_response
from flask_cors import CORS
import os
import json

app = Flask(__name__)
CORS(app) # enable CORS for all routes

@app.route('/history/<string:user>', methods=['GET', 'POST'])
def history(user):
    
    if request.method == 'OPTIONS':
        resp = make_response()
        resp.headers['Access-Control-Allow-Origin'] = '*'
        resp.headers['Access-Control-Allow-Methods'] = 'POST, GET'
        resp.headers['Access-Control-Allow-Headers'] = 'Content-Type'
        return resp

    if request.method == 'POST':
        # Save data to a file
        print(request.json)
        data = request.json
        filename = f"{user}.json"
        with open(filename, 'w') as f:
            f.write(json.dumps(data))
        
        resp = make_response(jsonify({"message": "Data saved successfully!"}))
        resp.headers['Access-Control-Allow-Origin'] = '*'
        return resp

    elif request.method == 'GET':
        # Read data from file and return

        filename = f"{user}.json"
        if os.path.isfile(filename):
            with open(filename, 'r') as f:
                data = f.read()
                resp = make_response(jsonify(data))
                resp.headers['Access-Control-Allow-Origin'] = '*'
                return resp
        else:
            # return jsonify({"message": "Data not found."})
            resp = make_response(jsonify({"message": "Data not found."}))
            resp.headers['Access-Control-Allow-Origin'] = '*'
            return resp


if __name__ == '__main__':
    # app.run(debug=True)
    app.run(host='0.0.0.0',port=64020, debug=True)
