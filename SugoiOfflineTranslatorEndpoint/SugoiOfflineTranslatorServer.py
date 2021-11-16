import os
import sys

fairseq_install_path = os.path.join(os.getcwd(), 'fairseq')
sys.path.insert(0, fairseq_install_path)

from flask import Flask
from flask import request
from flask_cors import CORS, cross_origin

import time
import json
from logging import getLogger

from fairseq.models.transformer import TransformerModel


LOG = getLogger("server")


ja2en = TransformerModel.from_pretrained(
    './fairseq/japaneseModel/',
    checkpoint_file='big.pretrain.pt',
    source_lang = "ja",
    target_lang = "en",
    bpe='sentencepiece',
    sentencepiece_model='./fairseq/spmModels/spm.ja.nopretok.model',
    # is_gpu=True
)

app = Flask(__name__)

cors = CORS(app)
app.config['CORS_HEADERS'] = 'Content-Type'

@app.route("/", methods = ['POST'])
@cross_origin()

def sendImage():
    tic = time.perf_counter()
    data = request.get_json()
    message = data.get("message")
    content = data.get("content").strip('﻿').strip();

    if (message == "close server"):
        shutdown_server()
        return

    if (message == "translate sentences"):
        t = ja2en.translate(content)

        toc = time.perf_counter()
        # LOG.info(f"Request: {content}")
        LOG.info(f"Translation {round(toc-tic,2)}s): {t}")

        return json.dumps(t)

    if (message == "translate batch"):
        batch = data.get("batch")
        if isinstance(batch, list):
            batch = [s.strip('﻿').strip() for s in batch]

            translated = [
                ja2en.translate(s)
                for s in batch
            ]

        toc = time.perf_counter()
        # LOG.info(f"Request: {batch}")
        LOG.info(f"Translation complete {round(toc-tic,2)}s)")

        return json.dumps(translated)


def shutdown_server():
    func = request.environ.get('werkzeug.server.shutdown')
    if func is None:
        raise RuntimeError('Not running with the Werkzeug Server')
    func()

if __name__ == "__main__":
    # monkey patch cli banner
    from flask import cli
    cli.show_server_banner = lambda *_: None

    port = int(sys.argv[1])
    cuda = (sys.argv[2] == "cuda")

    if cuda:
        LOG.info(f"Enabling cuda")
        ja2en.cuda()

    LOG.info(f"Running server on port {port}")
    app.run(host='127.0.0.1', port=port)
